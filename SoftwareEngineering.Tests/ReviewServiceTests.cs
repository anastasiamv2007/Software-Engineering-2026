using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;
using Xunit;

namespace SoftwareEngineering.Tests;

public class ReviewServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepoMock;
    private readonly Mock<IProjectRepository> _projectRepoMock;
    private readonly Mock<ICommentRepository> _commentRepoMock;
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _taskRepoMock = new Mock<ITaskRepository>();
        _projectRepoMock = new Mock<IProjectRepository>();
        _commentRepoMock = new Mock<ICommentRepository>();

        _service = new ReviewService(
            _taskRepoMock.Object,
            _projectRepoMock.Object,
            _commentRepoMock.Object);
    }

    [Fact]
    public async Task SubmitForReviewAsync_WhenCoExecutorAndTaskInProgress_SetsInReviewAndReturnsOk()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, Status = TaskItemStatus.InProgress };

        _taskRepoMock.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, userId)).ReturnsAsync(ProjectRole.CoExecutor);

        var result = await _service.SubmitForReviewAsync(task.Id, userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.InReview, task.Status);
        _taskRepoMock.Verify(r => r.UpdateAsync(task), Times.Once);
    }

    [Fact]
    public async Task SubmitForReviewAsync_WhenUserIsReviewer_ReturnsForbidden()
    {
        var projectId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, Status = TaskItemStatus.InProgress };

        _taskRepoMock.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, reviewerId)).ReturnsAsync(ProjectRole.Reviewer);

        var result = await _service.SubmitForReviewAsync(task.Id, reviewerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        _taskRepoMock.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Theory]
    [InlineData(TaskItemStatus.InReview)]
    [InlineData(TaskItemStatus.Done)]
    public async Task SubmitForReviewAsync_WhenTaskStatusIsInvalid_ReturnsValidation(TaskItemStatus status)
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, Status = status };

        _taskRepoMock.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, userId)).ReturnsAsync(ProjectRole.CoExecutor);

        var result = await _service.SubmitForReviewAsync(task.Id, userId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _taskRepoMock.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Theory]
    [InlineData(ProjectRole.Owner)]
    [InlineData(ProjectRole.Reviewer)]
    public async Task ApproveAsync_WhenOwnerOrReviewerAndTaskInReview_SetsDoneAndReturnsOk(ProjectRole role)
    {
        var projectId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, Status = TaskItemStatus.InReview };

        _taskRepoMock.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, reviewerId)).ReturnsAsync(role);

        var result = await _service.ApproveAsync(task.Id, reviewerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.Done, task.Status);
        _taskRepoMock.Verify(r => r.UpdateAsync(task), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_WhenCallerIsCoExecutor_ReturnsForbidden()
    {
        var projectId = Guid.NewGuid();
        var coExecutorId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, Status = TaskItemStatus.InReview };

        _taskRepoMock.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, coExecutorId)).ReturnsAsync(ProjectRole.CoExecutor);

        var result = await _service.ApproveAsync(task.Id, coExecutorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        _taskRepoMock.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.NeedsRevision)]
    [InlineData(TaskItemStatus.Done)]
    public async Task ApproveAsync_WhenTaskIsNotInReview_ReturnsValidation(TaskItemStatus status)
    {
        var projectId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, Status = status };

        _taskRepoMock.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, reviewerId)).ReturnsAsync(ProjectRole.Reviewer);

        var result = await _service.ApproveAsync(task.Id, reviewerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _taskRepoMock.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task RequestRevisionAsync_WhenCommentIsEmpty_ReturnsValidation()
    {
        var result = await _service.RequestRevisionAsync(Guid.NewGuid(), "   ", Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _taskRepoMock.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact]
    public async Task RequestRevisionAsync_WhenValid_SetsNeedsRevisionAndAddsComment()
    {
        var projectId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var task = new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, Status = TaskItemStatus.InReview };

        _taskRepoMock.Setup(r => r.GetByIdAsync(task.Id)).ReturnsAsync(task);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, reviewerId)).ReturnsAsync(ProjectRole.Reviewer);

        var result = await _service.RequestRevisionAsync(task.Id, "Додай перевірку граничних значень", reviewerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(TaskItemStatus.NeedsRevision, task.Status);
        _taskRepoMock.Verify(r => r.UpdateAsync(task), Times.Once);
        _commentRepoMock.Verify(r => r.AddAsync(It.Is<Comment>(c =>
            c.TaskId == task.Id &&
            c.AuthorId == reviewerId &&
            c.Content.Contains("Додай перевірку граничних значень"))), Times.Once);
    }

    [Fact]
    public async Task GetReviewQueueAsync_ReturnsOnlyInReviewTasksWhereUserIsOwnerOrReviewer()
    {
        var userId = Guid.NewGuid();
        var ownedProjectId = Guid.NewGuid();
        var reviewerProjectId = Guid.NewGuid();
        var coExecutorProjectId = Guid.NewGuid();

        var tasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), ProjectId = ownedProjectId, TaskCode = "PL-1", Title = "Власний проєкт", Status = TaskItemStatus.InReview },
            new() { Id = Guid.NewGuid(), ProjectId = reviewerProjectId, TaskCode = "PL-2", Title = "Рецензування", Status = TaskItemStatus.InReview },
            new() { Id = Guid.NewGuid(), ProjectId = coExecutorProjectId, TaskCode = "PL-3", Title = "Співвиконавець", Status = TaskItemStatus.InReview },
            new() { Id = Guid.NewGuid(), ProjectId = reviewerProjectId, TaskCode = "PL-4", Title = "В процесі", Status = TaskItemStatus.InProgress },
        };

        _taskRepoMock.Setup(r => r.GetByUserAsync(userId)).ReturnsAsync(tasks);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(ownedProjectId, userId)).ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(reviewerProjectId, userId)).ReturnsAsync(ProjectRole.Reviewer);
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(coExecutorProjectId, userId)).ReturnsAsync(ProjectRole.CoExecutor);

        var result = await _service.GetReviewQueueAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value!, item => item.TaskCode == "PL-1");
        Assert.Contains(result.Value!, item => item.TaskCode == "PL-2");
    }
}