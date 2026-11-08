using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class TaskAssignmentTests
{
    private readonly Mock<ITaskRepository> taskRepositoryMock = new();
    private readonly Mock<IProjectRepository> projectRepositoryMock = new();
    private readonly Mock<ICategoryRepository> categoryRepositoryMock = new();

    private readonly Guid projectId = Guid.NewGuid();
    private readonly Guid currentUserId = Guid.NewGuid();

    [Fact]
    public async Task AssignAsync_ProjectOwnerAssignsMember_SavesAssignee()
    {
        // Arrange
        var task = this.SetupTask();
        var assigneeId = Guid.NewGuid();
        this.SetupRole(this.currentUserId, ProjectRole.Owner);
        this.SetupRole(assigneeId, ProjectRole.CoExecutor);

        // Act
        var result = await this.CreateService().AssignAsync(task.Id, assigneeId, this.currentUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(assigneeId, result.Value!.AssigneeId);
        this.taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.Is<TaskItem>(t => t.AssigneeId == assigneeId)),
            Times.Once);
    }

    [Fact]
    public async Task AssignAsync_CoExecutorUnassigns_SavesNullAssignee()
    {
        // Arrange
        var task = this.SetupTask(Guid.NewGuid());
        this.SetupRole(this.currentUserId, ProjectRole.CoExecutor);

        // Act
        var result = await this.CreateService().AssignAsync(task.Id, null, this.currentUserId);

        // Assert
        Assert.True(result.IsSuccess);
        this.taskRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.Is<TaskItem>(t => t.AssigneeId == null)),
            Times.Once);
    }

    [Fact]
    public async Task AssignAsync_AssigneeIsNotProjectMember_ReturnsValidationAndDoesNotSave()
    {
        // Arrange
        var task = this.SetupTask();
        this.SetupRole(this.currentUserId, ProjectRole.Owner);

        // Act
        var result = await this.CreateService().AssignAsync(task.Id, Guid.NewGuid(), this.currentUserId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.taskRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Theory]
    [InlineData(ProjectRole.Reviewer)]
    [InlineData(null)]
    public async Task AssignAsync_UserCannotManage_ReturnsForbiddenAndDoesNotSave(ProjectRole? role)
    {
        // Arrange
        var task = this.SetupTask();
        this.SetupRole(this.currentUserId, role);

        // Act
        var result = await this.CreateService().AssignAsync(task.Id, Guid.NewGuid(), this.currentUserId);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        this.taskRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task AssignAsync_TaskNotFound_ReturnsNotFound()
    {
        // Act
        var result = await this.CreateService().AssignAsync(Guid.NewGuid(), Guid.NewGuid(), this.currentUserId);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task AssignAsync_EmptyIds_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var emptyTask = await service.AssignAsync(Guid.Empty, null, this.currentUserId);
        var emptyUser = await service.AssignAsync(Guid.NewGuid(), null, Guid.Empty);
        var emptyAssignee = await service.AssignAsync(Guid.NewGuid(), Guid.Empty, this.currentUserId);

        // Assert
        Assert.Equal(ErrorType.Validation, emptyTask.Error!.Type);
        Assert.Equal(ErrorType.Validation, emptyUser.Error!.Type);
        Assert.Equal(ErrorType.Validation, emptyAssignee.Error!.Type);
    }

    [Fact]
    public async Task AssignAsync_RepositoryFails_ReturnsUnexpectedWithoutThrowing()
    {
        // Arrange
        var task = this.SetupTask();
        var assigneeId = Guid.NewGuid();
        this.SetupRole(this.currentUserId, ProjectRole.Owner);
        this.SetupRole(assigneeId, ProjectRole.CoExecutor);
        this.taskRepositoryMock
            .Setup(repository => repository.UpdateAsync(It.IsAny<TaskItem>()))
            .ThrowsAsync(new InvalidOperationException());

        // Act
        var result = await this.CreateService().AssignAsync(task.Id, assigneeId, this.currentUserId);

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
    }

    [Fact]
    public async Task GetAssigneeCandidatesAsync_Owner_ReturnsProjectUsers()
    {
        // Arrange
        var task = this.SetupTask();
        this.SetupRole(this.currentUserId, ProjectRole.Owner);
        this.projectRepositoryMock
            .Setup(repository => repository.GetProjectUsersAsync(this.projectId))
            .ReturnsAsync(new List<User> { new() { Id = Guid.NewGuid(), FullName = "Іван", Email = "i@x.com" } });

        // Act
        var result = await this.CreateService().GetAssigneeCandidatesAsync(task.Id, this.currentUserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }

    private TaskService CreateService()
    {
        return new TaskService(
            this.taskRepositoryMock.Object,
            this.projectRepositoryMock.Object,
            this.categoryRepositoryMock.Object);
    }

    private TaskItem SetupTask(Guid? assigneeId = null)
    {
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = this.projectId, AssigneeId = assigneeId, Title = "Завдання", TaskCode = "PL-1" };
        this.taskRepositoryMock.Setup(repository => repository.GetByIdAsync(task.Id)).ReturnsAsync(task);

        return task;
    }

    private void SetupRole(Guid userId, ProjectRole? role)
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(this.projectId, userId))
            .ReturnsAsync(role);
    }
}