using Moq;
using SoftwareEngineering.BLL.Exceptions;
using SoftwareEngineering.BLL.Models;
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

    public TaskServiceTests()
    {
        // За замовчуванням користувач — співвиконавець проєкту
        this.SetProjectRole(ProjectRole.CoExecutor);
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsCodeBasedOnProjectTaskCount()
    {
        // Arrange
        const int existingTasksCount = 4;
        const string expectedTaskCode = "PL-5";
        this.repositoryMock
            .Setup(repository => repository.CountByProjectAsync(It.IsAny<Guid>()))
            .ReturnsAsync(existingTasksCount);
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(CreateValidModel());

        // Assert
        Assert.Equal(expectedTaskCode, result.TaskCode);
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsTaskWithNewId()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(CreateValidModel());

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
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
        Assert.Equal(model.Title, result.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateAsync_EmptyTitle_ThrowsValidationException(string? title)
    {
        // Arrange
        var model = CreateValidModel();
        model.Title = title!;
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_TitleLongerThanMax_ThrowsValidationException()
    {
        // Arrange
        var model = CreateValidModel();
        model.Title = new string('a', MaxTitleLength + 1);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_NoDeadline_ThrowsValidationException()
    {
        // Arrange
        var model = CreateValidModel();
        model.Deadline = null;
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_DeadlineInPast_ThrowsValidationException()
    {
        // Arrange
        var model = CreateValidModel();
        model.Deadline = DateTime.UtcNow.AddDays(-1);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_EmptyProjectId_ThrowsValidationException()
    {
        // Arrange
        var model = CreateValidModel();
        model.ProjectId = Guid.Empty;
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_NullModel_ThrowsValidationException()
    {
        // Arrange
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_InvalidModel_DoesNotSaveTask()
    {
        // Arrange
        var model = CreateValidModel();
        model.Title = string.Empty;
        var service = this.CreateService();

        // Act
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));

        // Assert
        this.repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RepositoryFails_PropagatesException()
    {
        // Arrange
        this.repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>()))
            .ThrowsAsync(new InvalidOperationException());
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CreateValidModel()));
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
        return new TaskService(this.repositoryMock.Object, this.projectRepositoryMock.Object);
    }

    private void SetProjectRole(ProjectRole? role)
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(role);
    }

    private void CaptureSavedTask()
    {
        this.repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>()))
            .Callback<TaskItem>(task => this.savedTask = task)
            .Returns(Task.CompletedTask);
    }

    // 2

    [Fact]
    public async Task GetByIdAsync_EmptyId_ThrowsValidationException()
    {
        var mockRepo = new Mock<ITaskRepository>();
        var service = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object);
        var emptyId = Guid.Empty;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.GetByIdAsync(emptyId));
        Assert.Contains("не може бути порожнім", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_TaskNotFound_ThrowsValidationException()
    {
        var mockRepo = new Mock<ITaskRepository>();
        var fakeId = Guid.NewGuid();

        mockRepo.Setup(repo => repo.GetByIdAsync(fakeId)).ReturnsAsync((TaskItem?)null);
        var service = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.GetByIdAsync(fakeId));
        Assert.Contains("не знайдено", exception.Message);
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
        var service = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object);

        var result = await service.GetByIdAsync(validId);

        Assert.NotNull(result);
        Assert.Equal(validId, result.Id);
        Assert.Equal("Тестове завдання", result.Title);
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
    public async Task UpdateAsync_EmptyTitle_ThrowsValidationException()
    {
        var invalidModel = new TaskModel
        {
            Id = Guid.NewGuid(),
            Title = string.Empty,
            Description = "Опис",
            Priority = TaskPriority.Low
        };
        var service = this.CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(invalidModel, Guid.NewGuid()));

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
    public async Task UpdateDeadlineAsync_UserIsAssigneeNotOwner_ThrowsValidationException()
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

        // Act and Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateDeadlineAsync(taskId, newDeadline, assigneeId));

        Assert.Contains("лише власник завдання або проєкту", exception.Message);
        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDeadlineAsync_DeadlineInPast_ThrowsValidationException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var pastDeadline = DateTime.UtcNow.AddDays(-1); 

        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateDeadlineAsync(taskId, pastDeadline, currentUserId));

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
        Assert.Equal(newStatus, result.Status);
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
        Assert.Equal(entity.Id, result.Id);
    }

    [Fact]
    public async Task UpdateProgressAsync_EmptyTaskId_ThrowsValidationException()
    {
        // Arrange
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(Guid.Empty, TaskItemStatus.Done, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateProgressAsync_EmptyUserId_ThrowsValidationException()
    {
        // Arrange
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(Guid.NewGuid(), TaskItemStatus.Done, Guid.Empty));
    }

    [Fact]
    public async Task UpdateProgressAsync_UndefinedStatus_ThrowsValidationException()
    {
        // Arrange
        var service = this.CreateService();
        const TaskItemStatus undefinedStatus = (TaskItemStatus)999;

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(Guid.NewGuid(), undefinedStatus, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateProgressAsync_TaskNotFound_ThrowsValidationException()
    {
        // Arrange
        this.repositoryMock
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((TaskItem?)null);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(Guid.NewGuid(), TaskItemStatus.Done, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateProgressAsync_UserIsNotAssignee_ThrowsValidationException()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out _);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateProgressAsync_TaskWithoutAssignee_ThrowsValidationException()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out _);
        entity.AssigneeId = null;
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateProgressAsync_SameStatus_ThrowsValidationException()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out var userId);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(entity.Id, entity.Status, userId));
    }

    [Fact]
    public async Task UpdateProgressAsync_UserIsNotAssignee_DoesNotUpdateTask()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out _);
        var service = this.CreateService();

        // Act
        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, Guid.NewGuid()));

        // Assert
        this.repositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProgressAsync_RepositoryFails_PropagatesException()
    {
        // Arrange
        var entity = this.CreateAssignedTask(out var userId);
        this.repositoryMock
            .Setup(repository => repository.UpdateAsync(It.IsAny<TaskItem>()))
            .ThrowsAsync(new InvalidOperationException());
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateProgressAsync(entity.Id, TaskItemStatus.Done, userId));
    }

    // Матриця прав: власник проєкту / співвиконавець

    [Fact]
    public async Task CreateAsync_UserIsNotProjectMember_ThrowsValidationException()
    {
        // Arrange
        this.SetProjectRole(null);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(CreateValidModel()));
        this.repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>()), Times.Never);
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
    public async Task UpdateAsync_CoExecutorChangesDeadlineOfOthersTask_ThrowsValidationException()
    {
        // Arrange
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid(), Title = "Завдання", Deadline = DateTime.UtcNow.AddDays(1) };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var model = new TaskModel { Id = task.Id, Title = "Завдання", Deadline = DateTime.UtcNow.AddDays(5) };
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(model, Guid.NewGuid()));
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
        Assert.Equal(task.Id, result.Id);
        this.repositoryMock.Verify(repository => repository.DeleteAsync(task), Times.Once);
    }

    [Fact]
    public async Task DeleteTaskByOwnerAsync_UserIsNotOwner_ThrowsValidationException()
    {
        // Arrange
        var task = new TaskItem { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        this.repositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.DeleteTaskByOwnerAsync(new TaskModel { Id = task.Id }, Guid.NewGuid()));
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
        Assert.Equal(model.OwnerId, result.OwnerId);
    }

    [Fact]
    public async Task CreateAsync_EmptyOwnerId_ThrowsValidationException()
    {
        // Arrange
        var model = CreateValidModel();
        model.OwnerId = Guid.Empty;
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(model));
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

        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object);

        // Act
        await taskService.DeleteTaskAsync(taskId, ownerId);

        // Assert
        mockRepo.Verify(repo => repo.DeleteAsync(taskId), Times.Once);
    }

    // Тест 2: Помилка доступу (користувач НЕ є творцем завдання)
    [Fact]
    public async Task DeleteTaskAsync_UserIsNotOwner_ThrowsException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid(); // Інший користувач
        var task = new TaskItem { Id = taskId, OwnerId = ownerId };

        var mockRepo = new Mock<ITaskRepository>();
        mockRepo.Setup(repo => repo.GetByIdAsync(taskId)).ReturnsAsync(task);

        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            taskService.DeleteTaskAsync(taskId, strangerId));

        mockRepo.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Тест 3: Завдання не знайдено в базі
    [Fact]
    public async Task DeleteTaskAsync_TaskDoesNotExist_ThrowsException()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockRepo = new Mock<ITaskRepository>();

        // Виправлено: додано знак питання (TaskItem?) для Nullable типів
        mockRepo.Setup(repo => repo.GetByIdAsync(taskId)).ReturnsAsync((TaskItem?)null);

        var taskService = new TaskService(mockRepo.Object, this.projectRepositoryMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            taskService.DeleteTaskAsync(taskId, userId));

        mockRepo.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }
}