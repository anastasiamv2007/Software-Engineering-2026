//using System;
//using System.Threading.Tasks;
//using Moq;
//using SoftwareEngineering.BLL.Results;
//using SoftwareEngineering.BLL.Services;
//using SoftwareEngineering.DAL.Entities;
//using SoftwareEngineering.DAL.Enums;
//using SoftwareEngineering.DAL.Repositories;
//using Xunit;

//namespace SoftwareEngineering.Tests;

//public class ProjectMemberServiceTests
//{
//    private readonly Mock<IProjectRepository> _projectRepoMock;
//    private readonly Mock<IUserRepository> _userRepoMock;
//    private readonly ProjectMemberService _service;

//    public ProjectMemberServiceTests()
//    {
//        _projectRepoMock = new Mock<IProjectRepository>();
//        _userRepoMock = new Mock<IUserRepository>();

//        _service = new ProjectMemberService(
//            _projectRepoMock.Object,
//            _userRepoMock.Object);
//    }

//    [Theory]
//    [InlineData("")]
//    [InlineData("   ")]
//    [InlineData("not-an-email")]
//    [InlineData(null)]
//    public async Task AddCoExecutorAsync_WhenEmailIsInvalid_ReturnsValidationResult(string? email)
//    {
//        // Act
//        var result = await _service.AddCoExecutorAsync(Guid.NewGuid(), email!, Guid.NewGuid());

//        // Assert
//        Assert.Equal(ErrorType.Validation, result.Error!.Type);
//        _userRepoMock.Verify(repo => repo.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
//        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
//    }

//    [Fact]
//    public async Task AddCoExecutorAsync_WhenRepositoryThrows_ReturnsUnexpectedResult()
//    {
//        // Arrange
//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
//            .ThrowsAsync(new InvalidOperationException("DB error"));

//        // Act
//        var result = await _service.AddCoExecutorAsync(Guid.NewGuid(), "user@planora.com", Guid.NewGuid());

//        // Assert
//        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
//    }

//    [Fact]
//    public async Task AddCoExecutorAsync_WhenUserIsNotOwner_ReturnsForbiddenResult()
//    {
//        // Arrange
//        var projectId = Guid.NewGuid();
//        var currentUserId = Guid.NewGuid();
//        var email = "test@planora.com";

//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
//            .ReturnsAsync(ProjectRole.CoExecutor); // Роль не Owner

//        // Act
//        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

//        // Assert
//        Assert.False(result.IsSuccess);
//        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
//        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
//    }

//    [Fact]
//    public async Task AddCoExecutorAsync_WhenTargetUserNotFound_ReturnsNotFoundResult()
//    {
//        // Arrange
//        var projectId = Guid.NewGuid();
//        var currentUserId = Guid.NewGuid();
//        var email = "notfound@planora.com";

//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
//            .ReturnsAsync(ProjectRole.Owner);
//        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
//            .ReturnsAsync((User?)null); // Користувача немає

//        // Act
//        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

//        // Assert
//        Assert.False(result.IsSuccess);
//        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
//        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
//    }

//    [Fact]
//    public async Task AddCoExecutorAsync_WhenTargetUserIsOwner_ReturnsValidationResult()
//    {
//        // Arrange
//        var projectId = Guid.NewGuid();
//        var currentUserId = Guid.NewGuid();
//        var email = "owner@planora.com";
//        var targetUser = new User { Id = currentUserId, Email = email }; // Той самий ID

//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
//            .ReturnsAsync(ProjectRole.Owner);
//        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
//            .ReturnsAsync(targetUser);

//        // Act
//        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

//        // Assert
//        Assert.False(result.IsSuccess);
//        Assert.Equal(ErrorType.Validation, result.Error!.Type);
//        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
//    }

//    [Fact]
//    public async Task AddCoExecutorAsync_WhenTargetUserIsAlreadyMember_ReturnsDuplicateResult()
//    {
//        // Arrange
//        var projectId = Guid.NewGuid();
//        var currentUserId = Guid.NewGuid();
//        var targetUserId = Guid.NewGuid();
//        var email = "already@planora.com";
//        var targetUser = new User { Id = targetUserId, Email = email };

//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
//            .ReturnsAsync(ProjectRole.Owner);
//        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
//            .ReturnsAsync(targetUser);
//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, targetUserId))
//            .ReturnsAsync(ProjectRole.CoExecutor); // Вже є учасником

//        // Act
//        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

//        // Assert
//        Assert.False(result.IsSuccess);
//        Assert.Equal(ErrorType.Duplicate, result.Error!.Type);
//        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.IsAny<ProjectMember>()), Times.Never);
//    }

//    [Fact]
//    public async Task AddCoExecutorAsync_WhenAllConditionsMet_AddsMemberAndReturnsSuccess()
//    {
//        // Arrange
//        var projectId = Guid.NewGuid();
//        var currentUserId = Guid.NewGuid();
//        var targetUserId = Guid.NewGuid();
//        var email = "newuser@planora.com";
//        var targetUser = new User { Id = targetUserId, Email = email };

//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
//            .ReturnsAsync(ProjectRole.Owner);
//        _userRepoMock.Setup(repo => repo.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
//            .ReturnsAsync(targetUser);
//        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, targetUserId))
//            .ReturnsAsync((ProjectRole?)null); // Ще не учасник

//        // Act
//        var result = await _service.AddCoExecutorAsync(projectId, email, currentUserId);

//        // Assert
//        Assert.True(result.IsSuccess);

//        // Перевіряємо, що метод збереження був викликаний рівно 1 раз із правильними даними
//        _projectRepoMock.Verify(repo => repo.AddMemberAsync(It.Is<ProjectMember>(m =>
//            m.ProjectId == projectId &&
//            m.UserId == targetUserId &&
//            m.Role == ProjectRole.CoExecutor)), Times.Once);
//    }

//    //

//}
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

    // --- Тести для ChangeRoleAsync та GetProjectMembersAsync ---

    [Fact]
    public async Task ChangeRoleAsync_WhenCallerIsNotOwner_ReturnsForbiddenResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, currentUserId))
            .ReturnsAsync(ProjectRole.CoExecutor);

        // Act
        var result = await _service.ChangeRoleAsync(projectId, memberId, ProjectRole.Reviewer, currentUserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        _projectRepoMock.Verify(
            repo => repo.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProjectRole>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenChangingOwnRole_ReturnsValidationResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, ownerId))
            .ReturnsAsync(ProjectRole.Owner);

        // Act
        var result = await _service.ChangeRoleAsync(projectId, ownerId, ProjectRole.Reviewer, ownerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _projectRepoMock.Verify(
            repo => repo.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProjectRole>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenNewRoleIsOwner_ReturnsValidationResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, ownerId))
            .ReturnsAsync(ProjectRole.Owner);

        // Act
        var result = await _service.ChangeRoleAsync(projectId, memberId, ProjectRole.Owner, ownerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        _projectRepoMock.Verify(
            repo => repo.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProjectRole>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenMemberNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, ownerId))
            .ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, memberId))
            .ReturnsAsync((ProjectRole?)null);

        // Act
        var result = await _service.ChangeRoleAsync(projectId, memberId, ProjectRole.Reviewer, ownerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        _projectRepoMock.Verify(
            repo => repo.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProjectRole>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenRoleIsUnchanged_ReturnsOkWithoutUpdatingDatabase()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, ownerId))
            .ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, memberId))
            .ReturnsAsync(ProjectRole.CoExecutor);

        // Act
        var result = await _service.ChangeRoleAsync(projectId, memberId, ProjectRole.CoExecutor, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
        _projectRepoMock.Verify(
            repo => repo.UpdateMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ProjectRole>()),
            Times.Never);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenAllConditionsMet_UpdatesRoleAndReturnsSuccess()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, ownerId))
            .ReturnsAsync(ProjectRole.Owner);
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(projectId, memberId))
            .ReturnsAsync(ProjectRole.CoExecutor);

        // Act
        var result = await _service.ChangeRoleAsync(projectId, memberId, ProjectRole.Reviewer, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
        _projectRepoMock.Verify(
            repo => repo.UpdateMemberRoleAsync(projectId, memberId, ProjectRole.Reviewer),
            Times.Once);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenRepositoryThrows_ReturnsUnexpectedResult()
    {
        // Arrange
        _projectRepoMock.Setup(repo => repo.GetMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act
        var result = await _service.ChangeRoleAsync(Guid.NewGuid(), Guid.NewGuid(), ProjectRole.Reviewer, Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
    }

    [Fact]
    public async Task GetProjectMembersAsync_WhenMembersExist_ReturnsMappedModels()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var members = new List<ProjectMember>
        {
            new()
            {
                ProjectId = projectId,
                UserId = ownerId,
                Role = ProjectRole.Owner,
                User = new User { Id = ownerId, FullName = "Турчин Павло", Email = "pavlo@planora.com" },
            },
            new()
            {
                ProjectId = projectId,
                UserId = memberId,
                Role = ProjectRole.Reviewer,
                User = new User { Id = memberId, FullName = "Ihor Lihvan", Email = "ihor@planora.com" },
            },
        };

        _projectRepoMock.Setup(repo => repo.GetProjectMembersAsync(projectId))
            .ReturnsAsync(members);

        // Act
        var result = await _service.GetProjectMembersAsync(projectId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Турчин Павло", result[0].FullName);
        Assert.Equal(ProjectRole.Owner, result[0].Role);
        Assert.Equal("Ihor Lihvan", result[1].FullName);
        Assert.Equal(ProjectRole.Reviewer, result[1].Role);
    }
}