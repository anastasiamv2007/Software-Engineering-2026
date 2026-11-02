using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;
using Xunit;

namespace SoftwareEngineering.Tests;

public class ProjectMemberServiceTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ILogger<ProjectMemberService>> _loggerMock;
    private readonly ProjectMemberService _service;

    public ProjectMemberServiceTests()
    {
        _projectRepoMock = new Mock<IProjectRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<ProjectMemberService>>();

        _service = new ProjectMemberService(
            _projectRepoMock.Object,
            _userRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AddCoExecutorAsync_WhenUserIsNotOwner_ReturnsForbiddenResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var email = "test@planora.com";

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.CoExecutor); // Роль не Owner

        // Act
        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
    }

    [Fact]
    public async Task AddCoExecutorAsync_WhenTargetUserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var email = "notfound@planora.com";

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);
        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null); // Користувача немає

        // Act
        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
    }

    [Fact]
    public async Task AddCoExecutorAsync_WhenTargetUserIsOwner_ReturnsValidationResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var email = "owner@planora.com";
        var targetUser = new User { Id = currentUserId, Email = email }; // Той самий ID

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);
        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        // Act
        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
    }

    [Fact]
    public async Task AddCoExecutorAsync_WhenTargetUserIsAlreadyMember_ReturnsDuplicateResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var email = "already@planora.com";
        var targetUser = new User { Id = targetUserId, Email = email };

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);
        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, targetUserId))
            .ReturnsAsync(ProjectRole.CoExecutor); // Вже є учасником

        // Act
        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Duplicate, result.Error!.Type);
        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
    }

    [Fact]
    public async Task AddCoExecutorAsync_WhenAllConditionsMet_AddsMemberAndReturnsSuccess()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var email = "newuser@planora.com";
        var targetUser = new User { Id = targetUserId, Email = email };

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);
        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, targetUserId))
            .ReturnsAsync((ProjectRole?)null); // Ще не учасник

        // Act
        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

        // Assert
        Assert.True(result.IsSuccess);

        // Перевіряємо, що метод збереження був викликаний рівно 1 раз із правильними даними
        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.Is<ProjectMember>(m =>
            m.ProjectId == projectId &&
            m.UserId == targetUserId &&
            m.Role == ProjectRole.CoExecutor)), Times.Once);
    }
}