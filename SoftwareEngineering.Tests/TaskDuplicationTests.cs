using Moq;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class TaskDuplicationTests
{
    private readonly Mock<ITaskRepository> taskRepositoryMock = new();
    private readonly Mock<IProjectRepository> projectRepositoryMock = new();
    private readonly Mock<ICategoryRepository> categoryRepositoryMock = new();

    private readonly Guid userId = Guid.NewGuid();
    private readonly TaskItem source;
    private TaskItem? savedCopy;

    public TaskDuplicationTests()
    {
        this.source = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            AssigneeId = Guid.NewGuid(),
            TaskCode = "PL-3",
            Title = "Лабораторна 1",
            Description = "Звіт і презентація",
            Priority = TaskPriority.High,
            CategoryId = 7,
            Status = TaskItemStatus.Done,
            Deadline = DateTime.UtcNow.AddDays(-5),
        };
        this.source.Subtasks.Add(new Subtask { Id = Guid.NewGuid(), TaskId = this.source.Id, Title = "Звіт", IsCompleted = true });
        this.source.Subtasks.Add(new Subtask { Id = Guid.NewGuid(), TaskId = this.source.Id, Title = "Презентація", IsCompleted = false });
        this.source.Comments.Add(new Comment { Id = Guid.NewGuid(), TaskId = this.source.Id, Content = "Здано" });

        this.taskRepositoryMock.Setup(repository => repository.GetDetailsAsync(this.source.Id)).ReturnsAsync(this.source);
        this.taskRepositoryMock
            .Setup(repository => repository.GetTaskCodesAsync(this.source.ProjectId))
            .ReturnsAsync(new[] { "PL-1", "PL-2", "PL-3", "PL-4" });
        this.taskRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>()))
            .Callback<TaskItem>(task => this.savedCopy = task)
            .Returns(Task.CompletedTask);

        // За замовчуванням користувач — співвиконавець проєкту
        this.SetRole(ProjectRole.CoExecutor);
    }

    // Копія — це нове завдання: новий Id і код, статус «До виконання», власник — той, хто копіював
    [Fact]
    public async Task DuplicateAsync_ValidData_CreatesNewTodoTaskOwnedByCurrentUser()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.DuplicateAsync(this.source.Id, CreateModel(), this.userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(this.source.Id, this.savedCopy!.Id);
        Assert.Equal("PL-5", this.savedCopy.TaskCode);
        Assert.Equal(TaskItemStatus.Todo, this.savedCopy.Status);
        Assert.Equal(this.userId, this.savedCopy.OwnerId);
        Assert.Equal(this.userId, this.savedCopy.CreatorId);
        Assert.Equal(this.source.ProjectId, this.savedCopy.ProjectId);
    }

    // Шаблонні поля переносяться з оригіналу, а назва і дедлайн — з форми
    [Fact]
    public async Task DuplicateAsync_CopiesDescriptionPriorityAndCategory_TakesTitleAndDeadlineFromModel()
    {
        // Arrange
        var service = this.CreateService();
        var model = CreateModel();

        // Act
        await service.DuplicateAsync(this.source.Id, model, this.userId);

        // Assert
        Assert.Equal("Лабораторна 2", this.savedCopy!.Title);
        Assert.Equal(model.Deadline!.Value.ToUniversalTime(), this.savedCopy.Deadline);
        Assert.Equal(this.source.Description, this.savedCopy.Description);
        Assert.Equal(TaskPriority.High, this.savedCopy.Priority);
        Assert.Equal(7, this.savedCopy.CategoryId);
    }

    // Виконавець і коментарі належать історії оригіналу — у копію не потрапляють
    [Fact]
    public async Task DuplicateAsync_DoesNotCopyAssigneeOrComments()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        await service.DuplicateAsync(this.source.Id, CreateModel(), this.userId);

        // Assert
        Assert.Null(this.savedCopy!.AssigneeId);
        Assert.Empty(this.savedCopy.Comments);
    }

    // Підзадачі копіюються новими записами, і всі — невиконані
    [Fact]
    public async Task DuplicateAsync_CopySubtasks_CopiesThemAsNewAndUncompleted()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        await service.DuplicateAsync(this.source.Id, CreateModel(copySubtasks: true), this.userId);

        // Assert
        var subtasks = this.savedCopy!.Subtasks.ToList();
        Assert.Equal(new[] { "Звіт", "Презентація" }, subtasks.Select(subtask => subtask.Title));
        Assert.All(subtasks, subtask => Assert.False(subtask.IsCompleted));
        Assert.All(subtasks, subtask => Assert.Equal(this.savedCopy.Id, subtask.TaskId));
        Assert.DoesNotContain(subtasks, subtask => this.source.Subtasks.Any(original => original.Id == subtask.Id));
    }

    [Fact]
    public async Task DuplicateAsync_WithoutSubtasks_CreatesTaskWithoutSubtasks()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        await service.DuplicateAsync(this.source.Id, CreateModel(copySubtasks: false), this.userId);

        // Assert
        Assert.Empty(this.savedCopy!.Subtasks);
    }

    // Оригінал при копіюванні не змінюється
    [Fact]
    public async Task DuplicateAsync_DoesNotModifySource()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        await service.DuplicateAsync(this.source.Id, CreateModel(), this.userId);

        // Assert
        Assert.Equal(TaskItemStatus.Done, this.source.Status);
        Assert.True(this.source.Subtasks.First().IsCompleted);
        this.taskRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DuplicateAsync_EmptyTitle_ReturnsValidationAndDoesNotSave(string title)
    {
        // Arrange
        var service = this.CreateService();
        var model = CreateModel();
        model.Title = title;

        // Act
        var result = await service.DuplicateAsync(this.source.Id, model, this.userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.taskRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateAsync_DeadlineInPast_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();
        var model = CreateModel();
        model.Deadline = DateTime.Now.AddHours(-1);

        // Act
        var result = await service.DuplicateAsync(this.source.Id, model, this.userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task DuplicateAsync_NoDeadline_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();
        var model = CreateModel();
        model.Deadline = null;

        // Act
        var result = await service.DuplicateAsync(this.source.Id, model, this.userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task DuplicateAsync_SourceNotFound_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.DuplicateAsync(Guid.NewGuid(), CreateModel(), this.userId);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task DuplicateAsync_UserOutsideProject_ReturnsForbidden()
    {
        // Arrange
        this.SetRole(null);
        var service = this.CreateService();

        // Act
        var result = await service.DuplicateAsync(this.source.Id, CreateModel(), this.userId);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        this.taskRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateAsync_ReviewerRole_ReturnsForbidden()
    {
        // Arrange
        this.SetRole(ProjectRole.Reviewer);
        var service = this.CreateService();

        // Act
        var result = await service.DuplicateAsync(this.source.Id, CreateModel(), this.userId);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    // Помилка БД не вилітає винятком у UI, а повертається як Result без технічних деталей
    [Fact]
    public async Task DuplicateAsync_RepositoryThrows_ReturnsUnexpected()
    {
        // Arrange
        this.taskRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>()))
            .ThrowsAsync(new InvalidOperationException("duplicate key value violates unique constraint"));
        var service = this.CreateService();

        // Act
        var result = await service.DuplicateAsync(this.source.Id, CreateModel(), this.userId);

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
        Assert.DoesNotContain("unique constraint", result.Error.Message);
    }

    [Fact]
    public async Task DuplicateAsync_NullModel_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.DuplicateAsync(this.source.Id, null!, this.userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    private static DuplicateTaskModel CreateModel(bool copySubtasks = true)
    {
        return new DuplicateTaskModel
        {
            Title = "  Лабораторна 2  ",
            Deadline = DateTime.Now.AddDays(3),
            CopySubtasks = copySubtasks,
        };
    }

    private void SetRole(ProjectRole? role)
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(this.source.ProjectId, this.userId))
            .ReturnsAsync(role);
    }

    private TaskService CreateService()
    {
        return new TaskService(this.taskRepositoryMock.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);
    }
}
