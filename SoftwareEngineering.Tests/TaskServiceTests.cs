using Moq;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class TaskServiceTests
{
    private const int MaxTitleLength = 200;

    private readonly Mock<ITaskRepository> repositoryMock = new(); private TaskItem? savedTask;

    private readonly Mock<IProjectRepository> projectRepositoryMock = new();

    private readonly Mock<ICategoryRepository> categoryRepositoryMock = new();

    public TaskServiceTests()
    {
        // За замовчуванням користувач — співвиконавець проєкту
        this.SetProjectRole(ProjectRole.CoExecutor);

        // За замовчуванням у проєкті ще немає завдань
        this.repositoryMock.SetupAdd();
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsCodeAfterHighestExistingNumber()
    {
        // Arrange
        const string expectedTaskCode = "PL-5";
        this.repositoryMock.SetupAdd(new[] { "PL-1", "PL-2", "PL-3", "PL-4" });
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(CreateValidModel());

        // Assert
        Assert.Equal(expectedTaskCode, result.Value!.TaskCode);
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsTaskWithNewId()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(CreateValidModel());

        // Assert
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
    }

    [Fact]
    public async Task CreateAsync_ValidModel_SavesTaskWithTodoStatus()
    {
        // Arrange
        this.CaptureSavedTask();
        var service = this.CreateService();

        // Act
        await service.CreateAsync(CreateValidModel());

        // Assert
        Assert.Equal(TaskItemStatus.Todo, this.savedTask!.Status);
    }

    [Theory]
    [InlineData(TaskPriority.High)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.Low)]
    public async Task CreateAsync_AnyPriority_SavesTaskWithSamePriority(TaskPriority priority)
    {
        // Arrange
        this.CaptureSavedTask();
        var model = CreateValidModel();
        model.Priority = priority;
        var service = this.CreateService();

        // Act
        await service.CreateAsync(model);

        // Assert
        Assert.Equal(priority, this.savedTask!.Priority);
    }

    private TaskItem CreateAssignedTask(out Guid assigneeId)
    {
        assigneeId = Guid.NewGuid();
        var entity = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            TaskCode = "PL-1",
            Title = "Описати вимоги",
            AssigneeId = assigneeId,
            Status = TaskItemStatus.Todo,
        };

        this.repositoryMock
            .Setup(repository => repository.GetByIdAsync(entity.Id))
            .ReturnsAsync(entity);

        return entity;
    }

    [Fact]
    public async Task CreateAsync_TitleWithSpaces_SavesTrimmedTitle()
    {
        // Arrange
        const string titleWithSpaces = "  Описати вимоги  ";
        const string expectedTitle = "Описати вимоги";
        this.CaptureSavedTask();
        var model = CreateValidModel();
        model.Title = titleWithSpaces;
        var service = this.CreateService();

        // Act
        await service.CreateAsync(model);

        // Assert
        Assert.Equal(expectedTitle, this.savedTask!.Title);
    }

    [Fact]
    public async Task CreateAsync_TitleOfMaxLength_CreatesTask()
    {
        // Arrange
        var model = CreateValidModel();
        model.Title = new string('a', MaxTitleLength);
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(model);

        // Assert
        Assert.Equal(model.Title, result.Value!.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateAsync_EmptyTitle_Fails(string? title)
    {
        // Arrange
        var model = CreateValidModel();
        model.Title = title!;
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(model)).Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_TitleLongerThanMax_Fails()
    {
        // Arrange
        var model = CreateValidModel();
        model.Title = new string('a', MaxTitleLength + 1);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(model)).Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_NoDeadline_Fails()
    {
        // Arrange
        var model = CreateValidModel();
        model.Deadline = null;
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(model)).Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_DeadlineInPast_Fails()
    {
        // Arrange
        var model = CreateValidModel();
        model.Deadline = DateTime.UtcNow.AddDays(-1);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(model)).Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_EmptyProjectId_Fails()
    {
        // Arrange
        var model = CreateValidModel();
        model.ProjectId = Guid.Empty;
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(model)).Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_NullModel_Fails()
    {
        // Arrange
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(null!)).Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_InvalidModel_DoesNotSaveTask()
    {
        // Arrange
        var model = CreateValidModel();
        model.Title = string.Empty;
        var service = this.CreateService();

        // Act
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(model)).Error?.Type);

        // Assert
        this.repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<Func<IReadOnlyList<string>, string>>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ExistingCategory_SavesTaskWithCategory()
    {
        // Arrange
        this.CaptureSavedTask();
        this.categoryRepositoryMock
            .Setup(repository => repository.GetByIdAsync(3))
            .ReturnsAsync(new Category { Id = 3, Name = "Навчання", ColorHex = "#3B82F6" });
        var model = CreateValidModel();
        model.CategoryId = 3;
        var service = this.CreateService();

        // Act
        await service.CreateAsync(model);

        // Assert
        Assert.Equal(3, this.savedTask!.CategoryId);
    }

    [Fact]
    public async Task CreateAsync_MissingCategory_ReturnsNotFoundAndDoesNotSave()
    {
        // Arrange
        var model = CreateValidModel();
        model.CategoryId = 42;
        var service = this.CreateService();

        // Act
        Assert.Equal(ErrorType.NotFound, (await service.CreateAsync(model)).Error?.Type);

        // Assert
        this.repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<Func<IReadOnlyList<string>, string>>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_TaskWithCategory_MapsCategoryNameAndColor()
    {
        // Arrange
        this.repositoryMock
            .Setup(repository => repository.GetAsync())
            .ReturnsAsync(new[]
            {
                new TaskItem
                {
                    Id = Guid.NewGuid(),
                    Title = "Звіт",
                    CategoryId = 1,
                    Category = new Category { Id = 1, Name = "Робота", ColorHex = "#EF4444" },
                },
            });
        var service = this.CreateService();

        // Act
        var result = await service.GetAllAsync();

        // Assert
        Assert.Equal("Робота", result.Value![0].CategoryName);
        Assert.Equal("#EF4444", result.Value![0].CategoryColorHex);
    }

    [Fact]
    public async Task GetUserTasksAsync_ReturnsOnlyTasksFromRepositoryForThatUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        this.repositoryMock
            .Setup(repository => repository.GetByUserAsync(userId))
            .ReturnsAsync(new List<TaskItem> { new() { Id = Guid.NewGuid(), Title = "Моє", TaskCode = "PL-1" } });
        var service = this.CreateService();

        // Act
        var result = await service.GetUserTasksAsync(userId);

        // Assert
        Assert.Single(result.Value!);
        this.repositoryMock.Verify(repository => repository.GetAsync(), Times.Never);
    }

    [Fact]
    public async Task GetDetailsAsync_ProjectOwner_ReturnsFullInfoAndAllRights()
    {
        // Arrange
        var (task, owner, coExecutor) = CreateDetailedTask();
        this.repositoryMock.Setup(repository => repository.GetDetailsAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act
        var result = await service.GetDetailsAsync(task.Id, owner.Id);

        // Assert
        Assert.Equal("Лаб", result.Value!.ProjectName);
        Assert.Equal(owner.FullName, result.Value!.ProjectOwnerName);
        Assert.Equal(coExecutor.FullName, result.Value!.OwnerName);
        Assert.Equal(1, result.Value!.SubtasksCompleted);
        Assert.Equal(2, result.Value!.SubtasksTotal);
        Assert.Equal(2, result.Value!.Members.Count);
        Assert.Equal(ProjectRole.Owner, result.Value!.CurrentUserRole);
        Assert.True(result.Value!.CanDeleteOrChangeDeadline);
    }

    [Fact]
    public async Task GetDetailsAsync_CoExecutorOnForeignTask_CanEditButNotManage()
    {
        // Arrange
        var (task, owner, _) = CreateDetailedTask();
        task.OwnerId = owner.Id;
        task.Owner = owner;
        var otherCoExecutor = new User { Id = Guid.NewGuid(), FullName = "Інший", Email = "other@test.com" };
        task.Project.Members.Add(new ProjectMember { UserId = otherCoExecutor.Id, User = otherCoExecutor, Role = ProjectRole.CoExecutor });
        this.repositoryMock.Setup(repository => repository.GetDetailsAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act
        var result = await service.GetDetailsAsync(task.Id, otherCoExecutor.Id);

        // Assert
        Assert.True(result.Value!.CanEdit);
        Assert.False(result.Value!.CanDeleteOrChangeDeadline);
    }

    [Fact]
    public async Task GetDetailsAsync_UserOutsideProject_Fails()
    {
        // Arrange
        var (task, _, _) = CreateDetailedTask();
        this.repositoryMock.Setup(repository => repository.GetDetailsAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Forbidden, (await service.GetDetailsAsync(task.Id, Guid.NewGuid())).Error?.Type);
    }

    private static (TaskItem Task, User Owner, User CoExecutor) CreateDetailedTask()
    {
        var owner = new User { Id = Guid.NewGuid(), FullName = "Турчин Павло", Email = "pavlo@test.com" };
        var coExecutor = new User { Id = Guid.NewGuid(), FullName = "Ihor Lihvan", Email = "ihor@test.com" };
        var project = new Project { Id = Guid.NewGuid(), Name = "Лаб", OwnerId = owner.Id, Owner = owner };
        project.Members.Add(new ProjectMember { UserId = owner.Id, User = owner, Role = ProjectRole.Owner });
        project.Members.Add(new ProjectMember { UserId = coExecutor.Id, User = coExecutor, Role = ProjectRole.CoExecutor });

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Project = project,
            TaskCode = "PL-1",
            Title = "лаб1",
            OwnerId = coExecutor.Id,
            Owner = coExecutor,
            CreatorId = coExecutor.Id,
            Deadline = DateTime.UtcNow.AddDays(1),
        };
        task.Subtasks.Add(new Subtask { Title = "a", IsCompleted = true });
        task.Subtasks.Add(new Subtask { Title = "b" });

        return (task, owner, coExecutor);
    }

    private static TaskModel CreateValidModel()
    {
        return new TaskModel
        {
            ProjectId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Описати вимоги",
            Deadline = DateTime.UtcNow.AddDays(2),
        };
    }

    private TaskService CreateService()
    {
        return new TaskService(this.repositoryMock.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);
    }

    private void SetProjectRole(ProjectRole? role)
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(role);
    }

    private void CaptureSavedTask()
    {
        this.repositoryMock.SetupAdd(onSaved: task => this.savedTask = task);
    }

    // 2

    [Fact]
    public async Task GetByIdAsync_EmptyId_Fails()
    {
        var mockRepo = new Mock<ITaskRepository>();
        var service = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);
        var emptyId = Guid.Empty;

        var failed = await service.GetByIdAsync(emptyId);
        Assert.Equal(ErrorType.Validation, failed.Error?.Type);
        Assert.Contains("не може бути порожнім", failed.Error!.Message);
    }

    [Fact]
    public async Task GetByIdAsync_TaskNotFound_Fails()
    {
        var mockRepo = new Mock<ITaskRepository>();
        var fakeId = Guid.NewGuid();

        mockRepo.Setup(repo => repo.GetByIdAsync(fakeId)).ReturnsAsync((TaskItem?)null);
        var service = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);

        var failed = await service.GetByIdAsync(fakeId);
        Assert.Equal(ErrorType.NotFound, failed.Error?.Type);
        Assert.Contains("не знайдено", failed.Error!.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ValidId_ReturnsTaskModel()
    {
        var mockRepo = new Mock<ITaskRepository>();
        var validId = Guid.NewGuid();
        var fakeEntity = new TaskItem
        {
            Id = validId,
            Title = "Тестове завдання",
            ProjectId = Guid.NewGuid(),
            TaskCode = "PL-1",
            Status = TaskItemStatus.Todo
        };

        mockRepo.Setup(repo => repo.GetByIdAsync(validId)).ReturnsAsync(fakeEntity);
        var service = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);

        var result = await service.GetByIdAsync(validId);

        Assert.NotNull(result);
        Assert.Equal(validId, result.Value!.Id);
        Assert.Equal("Тестове завдання", result.Value!.Title);
    }

    // 3

    [Fact]
    public async Task UpdateAsync_ValidModel_CallsRepositoryUpdate()
    {

        var validModel = new TaskModel
        {
            Id = Guid.NewGuid(),
            Title = "Оновлене завдання",
            Description = "Оновлений опис",
            Priority = TaskPriority.High,
            Status = TaskItemStatus.Todo,
            Deadline = DateTime.UtcNow.AddDays(1)
        };


        var userId = Guid.NewGuid();
        var existingEntity = new TaskItem
        {
            Id = validModel.Id,
            ProjectId = Guid.NewGuid(),
            OwnerId = userId,
            Title = "Стара назва"
        };

        this.repositoryMock
            .Setup(repository => repository.GetByIdAsync(validModel.Id))
            .ReturnsAsync(existingEntity);

        var service = this.CreateService();


        await service.UpdateAsync(validModel, userId);


        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.Is<TaskItem>(
            t => t.Id == validModel.Id &&
                 t.Title == validModel.Title &&
                 t.Description == validModel.Description)), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_EmptyTitle_Fails()
    {
        var invalidModel = new TaskModel
        {
            Id = Guid.NewGuid(),
            Title = string.Empty,
            Description = "Опис",
            Priority = TaskPriority.Low
        };
        var service = this.CreateService();

        Assert.Equal(ErrorType.Validation, (await service.UpdateAsync(invalidModel, Guid.NewGuid())).Error?.Type);

        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    // 6
    [Fact]
    public async Task UpdateDeadlineAsync_UserIsOwner_UpdatesDeadlineAndCallsRepository()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var newDeadline = DateTime.UtcNow.AddDays(5);

        var existingEntity = new TaskItem
        {
            Id = taskId,
            ProjectId = Guid.NewGuid(),
            Title = "Спільне завдання",
            OwnerId = ownerId,
            AssigneeId = Guid.NewGuid(),
            Deadline = DateTime.UtcNow.AddDays(1)
        };

        this.repositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId))
            .ReturnsAsync(existingEntity);

        var service = this.CreateService();

        // Act
        var result = await service.UpdateDeadlineAsync(taskId, newDeadline, ownerId);

        // Assert
        Assert.NotNull(result);
        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.Is<TaskItem>(
            t => t.Id == taskId && t.Deadline == newDeadline)), Times.Once);
    }

    [Fact]
    public async Task UpdateDeadlineAsync_UserIsAssigneeNotOwner_Fails()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var newDeadline = DateTime.UtcNow.AddDays(5);

        var existingEntity = new TaskItem
        {
            Id = taskId,
            ProjectId = Guid.NewGuid(),
            Title = "Спільне завдання",
            OwnerId = Guid.NewGuid(),
            AssigneeId = assigneeId,
            Deadline = DateTime.UtcNow.AddDays(1)
        };

        this.repositoryMock
            .Setup(repository => repository.GetByIdAsync(taskId))
            .ReturnsAsync(existingEntity);

        var service = this.CreateService();

        // Act + Assert
        var failed = await service.UpdateDeadlineAsync(taskId, newDeadline, assigneeId);
        Assert.Equal(ErrorType.Forbidden, failed.Error?.Type);

        Assert.Contains("лише власник завдання або проєкту", failed.Error!.Message);
        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDeadlineAsync_DeadlineInPast_Fails()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var pastDeadline = DateTime.UtcNow.AddDays(-1);

        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.UpdateDeadlineAsync(taskId, pastDeadline, currentUserId)).Error?.Type);

        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    // 7

    [Theory]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public async Task UpdateProgressAsync_AssigneeAndNewStatus_SavesNewStatus(TaskItemStatus newStatus)
    {
        // Arrange
        var entity = this.CreateAssignedTask(out var userId);
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProgressAsync(entity.Id, newStatus, userId);

        // Assert
        Assert.Equal(newStatus, result.Value!.Status);
    }

    [Fact]
    public async Task UpdateProgressAsync_AssigneeAndNewStatus_CallsRepositoryUpdateOnce()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out var userId);
        var service = this.CreateService();

        // Act
        await service.UpdateProgressAsync(entity.Id, TaskItemStatus.InProgress, userId);

        // Assert
        this.repositoryMock.Verify(
            repository => repository.UpdateAsync(It.Is<TaskItem>(task => task.Status == TaskItemStatus.InProgress)),
            Times.Once);
    }

    [Fact]
    public async Task UpdateProgressAsync_ValidRequest_ReturnsModelWithSameId()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out var userId);
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, userId);

        // Assert
        Assert.Equal(entity.Id, result.Value!.Id);
    }

    [Fact]
    public async Task UpdateProgressAsync_EmptyTaskId_Fails()
    {
        // Arrange
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.UpdateProgressAsync(Guid.Empty, TaskItemStatus.Done, Guid.NewGuid())).Error?.Type);
    }

    [Fact]
    public async Task UpdateProgressAsync_EmptyUserId_Fails()
    {
        // Arrange
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.UpdateProgressAsync(Guid.NewGuid(), TaskItemStatus.Done, Guid.Empty)).Error?.Type);
    }

    [Fact]
    public async Task UpdateProgressAsync_UndefinedStatus_Fails()
    {
        // Arrange
        var service = this.CreateService();
        const TaskItemStatus undefinedStatus = (TaskItemStatus)999;

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.UpdateProgressAsync(Guid.NewGuid(), undefinedStatus, Guid.NewGuid())).Error?.Type);
    }

    [Fact]
    public async Task UpdateProgressAsync_TaskNotFound_Fails()
    {
        // Arrange
        this.repositoryMock
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((TaskItem?)null);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.NotFound, (await service.UpdateProgressAsync(Guid.NewGuid(), TaskItemStatus.Done, Guid.NewGuid())).Error?.Type);
    }

    [Fact]
    public async Task UpdateProgressAsync_UserIsNotProjectMember_Fails()
    {
        // Arrange
        this.SetProjectRole(null);
        var entity = this.CreateAssignedTask(out _);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Forbidden, (await service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, Guid.NewGuid())).Error?.Type);
    }

    [Fact]
    public async Task UpdateProgressAsync_ProjectOwnerAndTaskWithoutAssignee_UpdatesStatus()
    {
        // Arrange
        this.SetProjectRole(ProjectRole.Owner);
        var entity = this.CreateAssignedTask(out _);
        entity.AssigneeId = null;
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, Guid.NewGuid());

        // Assert
        Assert.Equal(TaskItemStatus.Done, result.Value!.Status);
        this.repositoryMock.Verify(repository => repository.UpdateAsync(entity), Times.Once);
    }

    [Fact]
    public async Task UpdateProgressAsync_SameStatus_Fails()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out var userId);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.UpdateProgressAsync(entity.Id, entity.Status, userId)).Error?.Type);
    }

    [Fact]
    public async Task UpdateProgressAsync_UserIsNotProjectMember_DoesNotUpdateTask()
    {
        // Arrange
        this.SetProjectRole(null);
        var entity = this.CreateAssignedTask(out _);
        var service = this.CreateService();

        // Act
        Assert.Equal(ErrorType.Forbidden, (await service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, Guid.NewGuid())).Error?.Type);

        // Assert
        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    // Матриця прав: власник проєкту / співвиконавець

    [Fact]
    public async Task CreateAsync_UserIsNotProjectMember_Fails()
    {
        // Arrange
        this.SetProjectRole(null);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Forbidden, (await service.CreateAsync(CreateValidModel())).Error?.Type);
        this.repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<Func<IReadOnlyList<string>, string>>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTaskAsync_ProjectOwnerDeletesCoExecutorTask_Deletes()
    {
        // Arrange
        this.SetProjectRole(ProjectRole.Owner);
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act
        await service.DeleteTaskAsync(task.Id, Guid.NewGuid());

        // Assert
        this.repositoryMock.Verify(repository => repository.DeleteAsync(task.Id), Times.Once);
    }

    [Fact]
    public async Task UpdateDeadlineAsync_ProjectOwnerOnCoExecutorTask_UpdatesDeadline()
    {
        // Arrange
        this.SetProjectRole(ProjectRole.Owner);
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "Завдання", TaskCode = "PL-1" };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act
        await service.UpdateDeadlineAsync(task.Id, DateTime.UtcNow.AddDays(3), Guid.NewGuid());

        // Assert
        this.repositoryMock.Verify(repository => repository.UpdateAsync(task), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_CoExecutorChangesDeadlineOfOthersTask_Fails()
    {
        // Arrange
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "Завдання", Deadline = DateTime.UtcNow.AddDays(1) };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var model = new TaskModel { Id = task.Id, Title = "Завдання", Deadline = DateTime.UtcNow.AddDays(5) };
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Forbidden, (await service.UpdateAsync(model, Guid.NewGuid())).Error?.Type);
        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_CoExecutorEditsTitleOfOthersTask_Updates()
    {
        // Arrange
        var deadline = DateTime.UtcNow.AddDays(1);
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "Стара назва", Deadline = deadline };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var model = new TaskModel { Id = task.Id, Title = "Нова назва", Deadline = deadline };
        var service = this.CreateService();

        // Act
        await service.UpdateAsync(model, Guid.NewGuid());

        // Assert
        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.Is<TaskItem>(t => t.Title == "Нова назва")), Times.Once);
    }

    // 4

    [Fact]
    public async Task DeleteTaskByOwnerAsync_UserIsOwner_DeletesTaskById()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = ownerId, Title = "Завдання", TaskCode = "PL-1" };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act
        var result = await service.DeleteTaskByOwnerAsync(new TaskModel { Id = task.Id }, ownerId);

        // Assert
        Assert.Equal(task.Id, result.Value!.Id);
        this.repositoryMock.Verify(repository => repository.DeleteAsync(task), Times.Once);
    }

    [Fact]
    public async Task DeleteTaskByOwnerAsync_UserIsNotOwner_Fails()
    {
        // Arrange
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Forbidden, (await service.DeleteTaskByOwnerAsync(new TaskModel { Id = task.Id }, Guid.NewGuid())).Error?.Type);
        this.repositoryMock.Verify(repository => repository.DeleteAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidModel_SavesOwnerAndCreator()
    {
        // Arrange
        this.CaptureSavedTask();
        var model = CreateValidModel();
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(model);

        // Assert
        Assert.Equal(model.OwnerId, this.savedTask!.OwnerId);
        Assert.Equal(model.OwnerId, this.savedTask.CreatorId);
        Assert.Equal(model.OwnerId, result.Value!.OwnerId);
    }

    [Fact]
    public async Task CreateAsync_EmptyOwnerId_Fails()
    {
        // Arrange
        var model = CreateValidModel();
        model.OwnerId = Guid.Empty;
        var service = this.CreateService();

        // Act + Assert
        Assert.Equal(ErrorType.Validation, (await service.CreateAsync(model)).Error?.Type);
    }

    // 5

    [Fact]
    public async Task DeleteTaskAsync_UserIsOwner_DeletesSuccessfully()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var task = new TaskItem { Id = taskId, OwnerId = ownerId };

        var mockRepo = new Mock<ITaskRepository>();
        mockRepo.Setup(repo => repo.GetByIdAsync(taskId)).ReturnsAsync(task);

        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);

        // Act
        await taskService.DeleteTaskAsync(taskId, ownerId);

        // Assert
        mockRepo.Verify(repo => repo.DeleteAsync(taskId), Times.Once);
    }

    // Тест 2: Помилка доступу (користувач НЕ є творцем завдання)
    [Fact]
    public async Task DeleteTaskAsync_UserIsNotOwner_ReturnsForbidden()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid(); // Інший користувач
        var task = new TaskItem { Id = taskId, OwnerId = ownerId };

        var mockRepo = new Mock<ITaskRepository>();
        mockRepo.Setup(repo => repo.GetByIdAsync(taskId)).ReturnsAsync(task);

        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);

        // Act & Assert
        Assert.Equal(ErrorType.Forbidden, (await taskService.DeleteTaskAsync(taskId, strangerId)).Error?.Type);

        mockRepo.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Тест 3: Завдання не знайдено в базі
    [Fact]
    public async Task DeleteTaskAsync_TaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockRepo = new Mock<ITaskRepository>();

        // Виправлено: додано знак питання (TaskItem?) для Nullable типів
        mockRepo.Setup(repo => repo.GetByIdAsync(taskId)).ReturnsAsync((TaskItem?)null);

        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);

        // Act & Assert
        Assert.Equal(ErrorType.NotFound, (await taskService.DeleteTaskAsync(taskId, userId)).Error?.Type);

        mockRepo.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTaskAsync_EmptyTaskId_Fails()
    {
        // Arrange
        var mockRepo = new Mock<ITaskRepository>();
        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);

        // Act & Assert
        Assert.Equal(ErrorType.Validation, (await taskService.DeleteTaskAsync(Guid.Empty, Guid.NewGuid())).Error?.Type);

        mockRepo.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTaskAsync_EmptyUserId_Fails()
    {
        // Arrange
        var mockRepo = new Mock<ITaskRepository>();
        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);

        // Act & Assert
        Assert.Equal(ErrorType.Validation, (await taskService.DeleteTaskAsync(Guid.NewGuid(), Guid.Empty)).Error?.Type);

        mockRepo.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }
}