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

    private readonly Mock<ITaskRepository> repositoryMock = new();

    private TaskItem? savedTask;

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
            Title = "Описати вимоги",
            Deadline = DateTime.UtcNow.AddDays(2),
        };
    }

    private TaskService CreateService()
    {
        return new TaskService(this.repositoryMock.Object);
    }

    private void CaptureSavedTask()
    {
        this.repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>()))
            .Callback<TaskItem>(task => this.savedTask = task)
            .Returns(Task.CompletedTask);
    }
}