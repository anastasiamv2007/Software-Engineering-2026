using Moq;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class AuthServiceSessionTests
{
    private const string Password = "password1";

    private readonly Mock<IUserRepository> userRepositoryMock = new();

    private readonly User user = new()
    {
        Id = Guid.NewGuid(),
        Email = "ihor@test.com",
        FullName = "Ihor Lihvan",
        PasswordHash = PasswordHasher.Hash(Password),
    };

    public AuthServiceSessionTests()
    {
        this.userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(this.user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => this.user);
        this.userRepositoryMock
            .Setup(repository => repository.GetByEmailAsync(this.user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => this.user);
    }

    // ---------- Вхід ----------

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsUser()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.LoginAsync(new LoginUserModel { Email = "  IHOR@test.com ", Password = Password });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(this.user.Id, result.Value!.Id);
    }

    // Невірний пароль і неіснуючий email — однакове повідомлення, щоб не підказувати, які email зареєстровані
    [Fact]
    public async Task LoginAsync_WrongPasswordAndUnknownEmail_ReturnSameMessage()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var wrongPassword = await service.LoginAsync(new LoginUserModel { Email = this.user.Email, Password = "wrong123" });
        var unknownEmail = await service.LoginAsync(new LoginUserModel { Email = "nobody@test.com", Password = Password });

        // Assert
        Assert.Equal(ErrorType.Validation, wrongPassword.Error!.Type);
        Assert.Equal(wrongPassword.Error.Message, unknownEmail.Error!.Message);
    }

    [Theory]
    [InlineData("", "password1")]
    [InlineData("ihor@test.com", "")]
    public async Task LoginAsync_EmptyFields_ReturnsValidation(string email, string password)
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.LoginAsync(new LoginUserModel { Email = email, Password = password });

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    // ---------- Реєстрація ----------

    [Fact]
    public async Task RegisterAsync_ValidModel_SavesNormalizedEmailAndHashedPassword()
    {
        // Arrange
        User? saved = null;
        this.userRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((created, _) => saved = created)
            .Returns(Task.CompletedTask);
        var service = this.CreateService();

        // Act
        var result = await service.RegisterAsync(new RegisterUserModel { FullName = " Нова Людина ", Email = " New@Test.com ", Password = "secret12" });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("new@test.com", saved!.Email);
        Assert.Equal("Нова Людина", saved.FullName);
        Assert.NotEqual("secret12", saved.PasswordHash);
        Assert.True(PasswordHasher.Verify("secret12", saved.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_EmailAlreadyExists_ReturnsDuplicate()
    {
        // Arrange
        this.userRepositoryMock
            .Setup(repository => repository.ExistsByEmailAsync(this.user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = this.CreateService();

        // Act
        var result = await service.RegisterAsync(new RegisterUserModel { FullName = "Дубль", Email = this.user.Email, Password = "secret12" });

        // Assert
        Assert.Equal(ErrorType.Duplicate, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "a@b.com", "secret12")]
    [InlineData("Ім'я", "not-an-email", "secret12")]
    [InlineData("Ім'я", "a@b.com", "123")]
    public async Task RegisterAsync_InvalidData_ReturnsValidation(string fullName, string email, string password)
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.RegisterAsync(new RegisterUserModel { FullName = fullName, Email = email, Password = password });

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- «Запам'ятати мене» ----------

    [Fact]
    public async Task RestoreSessionAsync_ValidSession_ReturnsUser()
    {
        // Arrange
        var service = this.CreateService();
        var session = (await service.CreateRememberedSessionAsync(this.user.Id)).Value!;

        // Act
        var restored = await service.RestoreSessionAsync(session);

        // Assert
        Assert.Equal(this.user.Id, restored.Value!.Id);
    }

    [Fact]
    public async Task CreateRememberedSessionAsync_DoesNotContainPasswordHash()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var session = (await service.CreateRememberedSessionAsync(this.user.Id)).Value!;

        // Assert
        Assert.NotEqual(this.user.PasswordHash, session.PasswordStamp);
        Assert.DoesNotContain(this.user.PasswordHash.Split(':')[1], session.PasswordStamp);
    }

    [Fact]
    public async Task CreateRememberedSessionAsync_UnknownUser_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.CreateRememberedSessionAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task RestoreSessionAsync_PasswordChanged_Fails()
    {
        // Arrange
        var service = this.CreateService();
        var session = (await service.CreateRememberedSessionAsync(this.user.Id)).Value!;
        this.user.PasswordHash = PasswordHasher.Hash("newPassword2");

        // Act
        var restored = await service.RestoreSessionAsync(session);

        // Assert
        Assert.True(restored.IsFailure);
    }

    [Fact]
    public async Task RestoreSessionAsync_UserNotInThisDatabase_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();
        var session = (await service.CreateRememberedSessionAsync(this.user.Id)).Value!;
        session.UserId = Guid.NewGuid();

        // Act
        var restored = await service.RestoreSessionAsync(session);

        // Assert
        Assert.Equal(ErrorType.NotFound, restored.Error!.Type);
    }

    private AuthService CreateService()
    {
        return new AuthService(this.userRepositoryMock.Object);
    }
}
