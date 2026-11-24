using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class StatisticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITaskRepository> repositoryMock = new();
    private readonly Guid userId = Guid.NewGuid();

    [Fact]
    public async Task GetStatisticsAsync_MixedTasks_CountsTotalCompletedActiveOverdue()
    {
        // Arrange
        this.SetupTasks(
            NewTask(TaskItemStatus.Done, Now.UtcDateTime.AddDays(-1)),
            NewTask(TaskItemStatus.InProgress, Now.UtcDateTime.AddDays(-1)),
            NewTask(TaskItemStatus.Todo, Now.UtcDateTime.AddDays(2)),
            NewTask(TaskItemStatus.Todo, null));

        // Act
        var result = await this.CreateService().GetStatisticsAsync(this.userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Total);
        Assert.Equal(1, result.Value.Completed);
        Assert.Equal(1, result.Value.InProgress);
        Assert.Equal(3, result.Value.Active);
        Assert.Equal(1, result.Value.Overdue);
    }

    [Fact]
    public async Task GetStatisticsAsync_DoneTaskWithPastDeadline_IsNotOverdue()
    {
        // Arrange
        this.SetupTasks(NewTask(TaskItemStatus.Done, Now.UtcDateTime.AddDays(-5)));

        // Act
        var result = await this.CreateService().GetStatisticsAsync(this.userId);

        // Assert
        Assert.Equal(0, result.Value!.Overdue);
    }

    [Fact]
    public async Task GetStatisticsAsync_NoTasks_ReturnsZerosAndZeroPercent()
    {
        // Arrange
        this.SetupTasks();

        // Act
        var result = await this.CreateService().GetStatisticsAsync(this.userId);

        // Assert
        Assert.Equal(0, result.Value!.Total);
        Assert.Equal(0, result.Value.CompletedPercent);
    }

    [Fact]
    public async Task GetStatisticsAsync_CompletedPercent_IsShareOfDoneTasks()
    {
        // Arrange
        this.SetupTasks(
            NewTask(TaskItemStatus.Done, null),
            NewTask(TaskItemStatus.Todo, null));

        // Act
        var result = await this.CreateService().GetStatisticsAsync(this.userId);

        // Assert
        Assert.Equal(50, result.Value!.CompletedPercent);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithProjectId_CountsOnlyThatProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var inProject = NewTask(TaskItemStatus.Todo, null);
        inProject.ProjectId = projectId;
        this.SetupTasks(inProject, NewTask(TaskItemStatus.Todo, null));

        // Act
        var result = await this.CreateService().GetStatisticsAsync(this.userId, projectId);

        // Assert
        Assert.Equal(1, result.Value!.Total);
    }

    [Fact]
    public async Task GetStatisticsAsync_EmptyUserId_ReturnsValidationAndDoesNotQuery()
    {
        // Act
        var result = await this.CreateService().GetStatisticsAsync(Guid.Empty);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.repositoryMock.Verify(repository => repository.GetByUserAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetStatisticsAsync_EmptyProjectId_ReturnsValidation()
    {
        // Act
        var result = await this.CreateService().GetStatisticsAsync(this.userId, Guid.Empty);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    private static TaskItem NewTask(TaskItemStatus status, DateTime? deadline)
    {
        return new TaskItem { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Status = status, Deadline = deadline, Title = "T", TaskCode = "PL-1" };
    }

    private StatisticsService CreateService()
    {
        return new StatisticsService(this.repositoryMock.Object, new FixedTimeProvider(Now));
    }

    private void SetupTasks(params TaskItem[] tasks)
    {
        this.repositoryMock
            .Setup(repository => repository.GetByUserAsync(this.userId))
            .ReturnsAsync(tasks.ToList());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}