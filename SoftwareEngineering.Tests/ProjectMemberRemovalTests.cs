using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class ProjectMemberRemovalTests
{
    private readonly Mock<IProjectRepository> projectRepositoryMock = new();
    private readonly Mock<IUserRepository> userRepositoryMock = new();

    private readonly Guid projectId = Guid.NewGuid();
    private readonly Guid ownerId = Guid.NewGuid();
    private readonly Guid memberId = Guid.NewGuid();

    [Fact]
    public async Task RemoveMemberAsync_OwnerRemovesMember_CallsRepositoryOnceAndSucceeds()
    {
        // Arrange
        this.SetupRole(this.ownerId, ProjectRole.Owner);
        this.SetupRole(this.memberId, ProjectRole.CoExecutor);

        // Act
        var result = await this.CreateService().RemoveMemberAsync(this.projectId, this.memberId, this.ownerId);

        // Assert
        Assert.True(result.IsSuccess);
        this.projectRepositoryMock.Verify(repository => repository.RemoveMemberAsync(this.projectId, this.memberId), Times.Once);
    }

    [Theory]
    [InlineData(ProjectRole.CoExecutor)]
    [InlineData(ProjectRole.Reviewer)]
    [InlineData(null)]
    public async Task RemoveMemberAsync_CallerIsNotOwner_ReturnsForbiddenAndDoesNotRemove(ProjectRole? role)
    {
        // Arrange
        var caller = Guid.NewGuid();
        this.SetupRole(caller, role);
        this.SetupRole(this.memberId, ProjectRole.CoExecutor);

        // Act
        var result = await this.CreateService().RemoveMemberAsync(this.projectId, this.memberId, caller);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        this.projectRepositoryMock.Verify(repository => repository.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RemoveMemberAsync_MemberNotInProject_ReturnsNotFound()
    {
        // Arrange
        this.SetupRole(this.ownerId, ProjectRole.Owner);
        this.SetupRole(this.memberId, null);

        // Act
        var result = await this.CreateService().RemoveMemberAsync(this.projectId, this.memberId, this.ownerId);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        this.projectRepositoryMock.Verify(repository => repository.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RemoveMemberAsync_OwnerRemovesHimself_ReturnsValidationWithHint()
    {
        // Arrange
        this.SetupRole(this.ownerId, ProjectRole.Owner);

        // Act
        var result = await this.CreateService().RemoveMemberAsync(this.projectId, this.ownerId, this.ownerId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("передайте власність", result.Error.Message);
        this.projectRepositoryMock.Verify(repository => repository.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RemoveMemberAsync_EmptyIds_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var emptyProject = await service.RemoveMemberAsync(Guid.Empty, this.memberId, this.ownerId);
        var emptyMember = await service.RemoveMemberAsync(this.projectId, Guid.Empty, this.ownerId);
        var emptyUser = await service.RemoveMemberAsync(this.projectId, this.memberId, Guid.Empty);

        // Assert
        Assert.Equal(ErrorType.Validation, emptyProject.Error!.Type);
        Assert.Equal(ErrorType.Validation, emptyMember.Error!.Type);
        Assert.Equal(ErrorType.Validation, emptyUser.Error!.Type);
    }

    [Fact]
    public async Task GetMembersAsync_Participant_ReturnsMappedMembers()
    {
        // Arrange
        this.SetupRole(this.memberId, ProjectRole.CoExecutor);
        this.projectRepositoryMock
            .Setup(repository => repository.GetMembersAsync(this.projectId))
            .ReturnsAsync(new List<ProjectMember>
            {
                new() { UserId = this.ownerId, Role = ProjectRole.Owner, User = new User { FullName = "Власник", Email = "o@x.com" } },
                new() { UserId = this.memberId, Role = ProjectRole.CoExecutor, User = new User { FullName = "Богдан", Email = "b@x.com" } },
            });

        // Act
        var result = await this.CreateService().GetMembersAsync(this.projectId, this.memberId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal(ProjectRole.Owner, result.Value[0].Role);
    }

    [Fact]
    public async Task GetMembersAsync_NotParticipant_ReturnsForbidden()
    {
        // Arrange
        var stranger = Guid.NewGuid();
        this.SetupRole(stranger, null);

        // Act
        var result = await this.CreateService().GetMembersAsync(this.projectId, stranger);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task GetMembersAsync_OwnerRowMissing_AddsProjectOwner()
    {
        // Arrange
        this.SetupRole(this.ownerId, ProjectRole.Owner);
        this.projectRepositoryMock
            .Setup(repository => repository.GetMembersAsync(this.projectId))
            .ReturnsAsync(new List<ProjectMember>());
        this.projectRepositoryMock
            .Setup(repository => repository.GetByIdAsync(this.projectId))
            .ReturnsAsync(new Project { Id = this.projectId, OwnerId = this.ownerId, Owner = new User { Id = this.ownerId, FullName = "Власник", Email = "o@x.com" } });

        // Act
        var result = await this.CreateService().GetMembersAsync(this.projectId, this.ownerId);

        // Assert
        Assert.Single(result.Value!);
        Assert.Equal(ProjectRole.Owner, result.Value![0].Role);
    }

    private ProjectMemberService CreateService()
    {
        return new ProjectMemberService(this.projectRepositoryMock.Object, this.userRepositoryMock.Object);
    }

    private void SetupRole(Guid userId, ProjectRole? role)
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(this.projectId, userId))
            .ReturnsAsync(role);
    }
}