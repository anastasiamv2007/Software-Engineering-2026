using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class SubtaskServiceTests
{
    private readonly Mock<ISubtaskRepository> subtaskRepositoryMock = new();
    private readonly Mock<ITaskRepository> taskRepositoryMock = new();
    private readonly Mock<IProjectRepository> projectRepositoryMock = new();

    private readonly Guid userId = Guid.NewGuid();

    private readonly TaskItem task = new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Title = "Лабораторна",
        TaskCode = "PL-1",
    };

    private readonly Subtask subtask;

    public SubtaskServiceTests()
    {
        this.subtask = new Subtask { Id = Guid.NewGuid(), TaskId = this.task.Id, Title = "Звіт", IsCompleted = false };

        this.taskRepositoryMock.Setup(repository => repository.GetByIdAsync(this.task.Id)).ReturnsAsync(this.task);
        this.subtaskRepositoryMock.Setup(repository => repository.GetByIdAsync(this.subtask.Id)).ReturnsAsync(this.subtask);

        // За замовчуванням користувач — співвиконавець проєкту
        this.SetRole(ProjectRole.CoExecutor);
    }

    [Fact]
    public async Task AddAsync_ValidTitle_SavesTrimmedSubtask()
    {
        // Arrange
        Subtask? saved = null;
        this.subtaskRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Subtask>()))
            .Callback<Subtask>(item => saved = item)
            .Returns(Task.CompletedTask);
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, "  Написати висновки  ", this.userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Написати висновки", saved!.Title);
        Assert.False(saved.IsCompleted);
        Assert.Equal(this.task.Id, saved.TaskId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddAsync_EmptyTitle_ReturnsValidationAndDoesNotSave(string title)
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, title, this.userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.subtaskRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Subtask>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_TitleLongerThanLimit_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();
        var title = new string('a', SubtaskService.MaxTitleLength + 1);

        // Act
        var result = await service.AddAsync(this.task.Id, title, this.userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task AddAsync_TitleExactlyAtLimitAfterTrim_Succeeds()
    {
        // Arrange
        var service = this.CreateService();
        var title = "  " + new string('a', SubtaskService.MaxTitleLength) + "  ";

        // Act
        var result = await service.AddAsync(this.task.Id, title, this.userId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AddAsync_TaskNotFound_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(Guid.NewGuid(), "Звіт", this.userId);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task AddAsync_UserOutsideProject_ReturnsForbidden()
    {
        // Arrange
        this.SetRole(null);
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, "Звіт", this.userId);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        this.subtaskRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Subtask>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_RepositoryThrows_ReturnsUnexpectedInsteadOfException()
    {
        // Arrange
        this.subtaskRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Subtask>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, "Звіт", this.userId);

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
        Assert.DoesNotContain("db down", result.Error.Message);
    }

    [Fact]
    public async Task SetCompletedAsync_CoExecutor_MarksSubtaskCompleted()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.SetCompletedAsync(this.subtask.Id, true, this.userId);

        // Assert
        Assert.True(result.Value!.IsCompleted);
        this.subtaskRepositoryMock.Verify(repository => repository.UpdateAsync(It.Is<Subtask>(item => item.IsCompleted)), Times.Once);
    }

    [Fact]
    public async Task SetCompletedAsync_AssigneeOutsideProject_IsAllowed()
    {
        // Arrange
        this.SetRole(null);
        this.task.AssigneeId = this.userId;
        var service = this.CreateService();

        // Act
        var result = await service.SetCompletedAsync(this.subtask.Id, true, this.userId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SetCompletedAsync_SameState_DoesNotWriteToDatabase()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.SetCompletedAsync(this.subtask.Id, false, this.userId);

        // Assert
        Assert.True(result.IsSuccess);
        this.subtaskRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<Subtask>()), Times.Never);
    }

    [Fact]
    public async Task SetCompletedAsync_SubtaskNotFound_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.SetCompletedAsync(Guid.NewGuid(), true, this.userId);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task DeleteAsync_CoExecutorOnForeignTask_ReturnsForbidden()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(this.subtask.Id, this.userId);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        this.subtaskRepositoryMock.Verify(repository => repository.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_CoExecutorOnOwnTask_Deletes()
    {
        // Arrange
        this.task.OwnerId = this.userId;
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(this.subtask.Id, this.userId);

        // Assert
        Assert.True(result.IsSuccess);
        this.subtaskRepositoryMock.Verify(repository => repository.DeleteAsync(this.subtask.Id), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ProjectOwner_Deletes()
    {
        // Arrange
        this.SetRole(ProjectRole.Owner);
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(this.subtask.Id, this.userId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetByTaskAsync_Member_ReturnsSubtasks()
    {
        // Arrange
        this.subtaskRepositoryMock
            .Setup(repository => repository.GetByTaskIdAsync(this.task.Id))
            .ReturnsAsync(new List<Subtask> { this.subtask, new() { Id = Guid.NewGuid(), TaskId = this.task.Id, Title = "Презентація", IsCompleted = true } });
        var service = this.CreateService();

        // Act
        var result = await service.GetByTaskAsync(this.task.Id, this.userId);

        // Assert
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal(1, result.Value.Count(item => item.IsCompleted));
    }

    [Fact]
    public async Task GetByTaskAsync_EmptyUserId_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.GetByTaskAsync(this.task.Id, Guid.Empty);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    private void SetRole(ProjectRole? role)
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(role);
    }

    private SubtaskService CreateService()
    {
        return new SubtaskService(this.subtaskRepositoryMock.Object, this.taskRepositoryMock.Object, this.projectRepositoryMock.Object);
    }
}
