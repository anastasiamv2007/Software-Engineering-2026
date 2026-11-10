using Moq;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class AuthServiceSessionTests
{
    private readonly Mock<IUserRepository> userRepositoryMock = new();

    private readonly User user = new()
    {
        Id = Guid.NewGuid(),
        Email = "ihor@test.com",
        FullName = "Ihor Lihvan",
        PasswordHash = PasswordHasher.Hash("password1"),
    };

    public AuthServiceSessionTests()
    {
        this.userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(this.user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => this.user);
    }

    [Fact]
    public async Task RestoreSessionAsync_ValidSession_ReturnsUser()
    {
        // Arrange
        var service = new AuthService(this.userRepositoryMock.Object);
        var session = await service.CreateRememberedSessionAsync(this.user.Id);

        // Act
        var restored = await service.RestoreSessionAsync(session);

        // Assert
        Assert.Equal(this.user.Id, restored!.Id);
    }

    [Fact]
    public async Task CreateRememberedSessionAsync_DoesNotContainPasswordHash()
    {
        // Arrange
        var service = new AuthService(this.userRepositoryMock.Object);

        // Act
        var session = await service.CreateRememberedSessionAsync(this.user.Id);

        // Assert
        Assert.NotEqual(this.user.PasswordHash, session.PasswordStamp);
        Assert.DoesNotContain(this.user.PasswordHash.Split(':')[1], session.PasswordStamp);
    }

    [Fact]
    public async Task RestoreSessionAsync_PasswordChanged_ReturnsNull()
    {
        // Arrange
        var service = new AuthService(this.userRepositoryMock.Object);
        var session = await service.CreateRememberedSessionAsync(this.user.Id);
        this.user.PasswordHash = PasswordHasher.Hash("newPassword2");

        // Act
        var restored = await service.RestoreSessionAsync(session);

        // Assert
        Assert.Null(restored);
    }

    [Fact]
    public async Task RestoreSessionAsync_UserNotInThisDatabase_ReturnsNull()
    {
        // Arrange
        var service = new AuthService(this.userRepositoryMock.Object);
        var session = await service.CreateRememberedSessionAsync(this.user.Id);
        session.UserId = Guid.NewGuid();

        // Act
        var restored = await service.RestoreSessionAsync(session);

        // Assert
        Assert.Null(restored);
    }
}
