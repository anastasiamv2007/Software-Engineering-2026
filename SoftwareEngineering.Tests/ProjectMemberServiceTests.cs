using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly ProjectMemberService _service;

    public ProjectMemberServiceTests()
    {
        _projectRepoMock = new Mock<IProjectRepository>();
        _userRepoMock = new Mock<IUserRepository>();

        _service = new ProjectMemberService(
            _projectRepoMock.Object,
            _userRepoMock.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData(null)]
    public async Task AddCoExecutorAsync_WhenEmailIsInvalid_ReturnsValidationResult(string? email)
    {
        // Act
        var result = await _service.AddCoExecutorAsync(Guid.NewGuid(), email!, Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _userRepoMock.Verify(repo => repo.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
    }

    [Fact]
    public async Task AddCoExecutorAsync_WhenRepositoryThrows_ReturnsUnexpectedResult()
    {
        // Arrange
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act
        var result = await _service.AddCoExecutorAsync(Guid.NewGuid(), "user@planora.com", Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
    }

    [Fact]
    public async Task AddCoExecutorAsync_WhenUserIsNotOwner_ReturnsForbiddenResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var email = "test@planora.com";

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.CoExecutor);

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
            .ReturnsAsync((User?)null);

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
        var targetUser = new User { Id = currentUserId, Email = email };

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
            .ReturnsAsync(ProjectRole.CoExecutor);

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
            .ReturnsAsync((ProjectRole?)null);

        // Act
        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

        // Assert
        Assert.True(result.IsSuccess);
        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.Is<ProjectMember>(m =>
            m.ProjectId == projectId &&
            m.UserId == targetUserId &&
            m.Role == ProjectRole.CoExecutor)), Times.Once);
    }

    // Тести для TransferOwnershipAsync 
    [Fact]
    public async Task TransferOwnershipAsync_WhenUserIsNotOwner_ReturnsForbiddenResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.CoExecutor);

        // Act
        var result = await _service.TransferOwnershipAsync(projectId, newOwnerId, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        _projectRepoMock.Verify(repo => repo.TransferOwnershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TransferOwnershipAsync_WhenNewOwnerIsSameAsCurrentOwner_ReturnsValidationResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);

        // Act
        var result = await _service.TransferOwnershipAsync(projectId, currentUserId, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _projectRepoMock.Verify(repo => repo.TransferOwnershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TransferOwnershipAsync_WhenNewOwnerIsNotProjectMember_ReturnsValidationResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, outsiderId))
            .ReturnsAsync((ProjectRole?)null);

        // Act
        var result = await _service.TransferOwnershipAsync(projectId, outsiderId, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _projectRepoMock.Verify(repo => repo.TransferOwnershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TransferOwnershipAsync_WhenRepositoryThrows_ReturnsUnexpectedResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, newOwnerId))
            .ReturnsAsync(ProjectRole.CoExecutor);
        _projectRepoMock.Setup(repo => repo.TransferOwnershipAsync(projectId, newOwnerId))
            .ThrowsAsync(new InvalidOperationException("Transaction failed"));

        // Act
        var result = await _service.TransferOwnershipAsync(projectId, newOwnerId, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
    }

    [Fact]
    public async Task TransferOwnershipAsync_WhenAllConditionsMet_TransfersOwnershipAndReturnsSuccess()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, newOwnerId))
            .ReturnsAsync(ProjectRole.CoExecutor);

        // Act
        var result = await _service.TransferOwnershipAsync(projectId, newOwnerId, currentUserId);

        // Assert
        Assert.True(result.IsSuccess);
        _projectRepoMock.Verify(repo => repo.TransferOwnershipAsync(projectId, newOwnerId), Times.Once);
    }

    [Fact]
    public async Task GetTransferCandidatesAsync_WhenOwnerCalls_ReturnsOnlyOtherMembers()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, ownerId))
            .ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(repo => repo.GetMembersAsync(projectId))
            .ReturnsAsync(new List<ProjectMember>
            {
                new() { UserId = ownerId, Role = ProjectRole.Owner, User = new User { Id = ownerId, FullName = "Owner User", Email = "owner@planora.com" } },
                new() { UserId = member1Id, Role = ProjectRole.CoExecutor, User = new User { Id = member1Id, FullName = "Member One", Email = "m1@planora.com" } },
                new() { UserId = member2Id, Role = ProjectRole.Reviewer, User = new User { Id = member2Id, FullName = "", Email = "m2@planora.com" } },
            });

        // Act
        var result = await _service.GetTransferCandidatesAsync(projectId, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.DoesNotContain(result.Value, c => c.UserId == ownerId);
        Assert.Contains(result.Value, c => c.UserId == member1Id && c.FullName == "Member One");
        Assert.Contains(result.Value, c => c.UserId == member2Id && c.FullName == "m2@planora.com");
    }
}