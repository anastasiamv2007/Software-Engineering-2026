using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class UserProfileTests
{
    private const string CurrentPassword = "password1";

    private readonly Mock<IUserRepository> userRepositoryMock = new();

    private readonly User user = new()
    {
        Id = Guid.NewGuid(),
        Email = "nazar@test.com",
        FullName = "Nazar Mykhalchuk",
        PasswordHash = PasswordHasher.Hash(CurrentPassword),
        CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
    };

    public UserProfileTests()
    {
        this.userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(this.user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => this.user);
        this.userRepositoryMock
            .Setup(repository => repository.GetByEmailAsync(this.user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => this.user);
    }

    // ---------- Оновлення профілю (UpdateProfileAsync) ----------

    [Fact]
    public async Task UpdateProfileAsync_ValidData_UpdatesUserAndNormalizesEmail()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProfileAsync(this.user.Id, "  Нове Ім'я  ", "  NEW.Email@Test.com  ");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Нове Ім'я", result.Value!.FullName);
        Assert.Equal("new.email@test.com", result.Value.Email);
        Assert.Equal("Нове Ім'я", this.user.FullName);
        Assert.Equal("new.email@test.com", this.user.Email);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(this.user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_SameUserEmail_ReturnsSuccess()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProfileAsync(this.user.Id, "Оновлене Ім'я", this.user.Email);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Оновлене Ім'я", result.Value!.FullName);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(this.user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", "valid@test.com")]
    [InlineData("   ", "valid@test.com")]
    [InlineData("Valid Name", "")]
    [InlineData("Valid Name", "not-an-email")]
    public async Task UpdateProfileAsync_InvalidData_ReturnsValidation(string fullName, string email)
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProfileAsync(this.user.Id, fullName, email);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Довжини як у стовпцях users: довше БД не прийме, тож відмова має прийти від сервісу, а не винятком від БД
    [Theory]
    [InlineData(151, 10)]
    [InlineData(10, 256)]
    public async Task UpdateProfileAsync_TooLongNameOrEmail_ReturnsValidation(int nameLength, int emailLength)
    {
        // Arrange
        var fullName = new string('І', nameLength);
        var email = new string('a', emailLength - "@test.com".Length) + "@test.com";
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProfileAsync(this.user.Id, fullName, email);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_EmailTakenByAnotherUser_ReturnsDuplicate()
    {
        // Arrange
        var otherUser = new User { Id = Guid.NewGuid(), Email = "taken@test.com", FullName = "Other User" };
        this.userRepositoryMock
            .Setup(repository => repository.GetByEmailAsync("taken@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUser);
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProfileAsync(this.user.Id, "Nazar", "taken@test.com");

        // Assert
        Assert.Equal(ErrorType.Duplicate, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_UnknownUser_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProfileAsync(Guid.NewGuid(), "Nazar", "nazar@test.com");

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Зміна пароля (ChangePasswordAsync) ----------

    [Fact]
    public async Task ChangePasswordAsync_ValidPasswords_HashesNewPasswordAndInvalidatesRememberedSession()
    {
        // Arrange
        var service = this.CreateService();
        var rememberedSession = (await service.CreateRememberedSessionAsync(this.user.Id)).Value!;

        // Act
        var changeResult = await service.ChangePasswordAsync(this.user.Id, CurrentPassword, "newSecret123");
        var restoreResult = await service.RestoreSessionAsync(rememberedSession);

        // Assert
        Assert.True(changeResult.IsSuccess);
        Assert.NotEqual("newSecret123", this.user.PasswordHash);
        Assert.True(PasswordHasher.Verify("newSecret123", this.user.PasswordHash));
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(this.user, It.IsAny<CancellationToken>()), Times.Once);

        // Збережена сесія «Запам'ятати мене» після зміни пароля має стати недійсною
        Assert.True(restoreResult.IsFailure);
        Assert.Equal(ErrorType.Validation, restoreResult.Error!.Type);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongOldPassword_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.ChangePasswordAsync(this.user.Id, "wrongOldPass", "newSecret123");

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.True(PasswordHasher.Verify(CurrentPassword, this.user.PasswordHash));
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "newSecret123")]
    [InlineData(CurrentPassword, "")]
    [InlineData(CurrentPassword, "12345")]
    [InlineData(CurrentPassword, CurrentPassword)]
    public async Task ChangePasswordAsync_InvalidPasswords_ReturnsValidation(string oldPassword, string newPassword)
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.ChangePasswordAsync(this.user.Id, oldPassword, newPassword);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_UnknownUser_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.ChangePasswordAsync(Guid.NewGuid(), CurrentPassword, "newSecret123");

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        this.userRepositoryMock.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private AuthService CreateService()
    {
        return new AuthService(this.userRepositoryMock.Object);
    }
}