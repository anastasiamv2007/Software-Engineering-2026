using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;

namespace SoftwareEngineering.Tests;

public class UserSessionTests
{
    [Fact]
    public void SetUser_ValidUser_IsAuthenticatedWithUserId()
    {
        // Arrange
        var session = new UserSession();
        var user = new UserModel { Id = Guid.NewGuid(), FullName = "Іван Петренко" };

        // Act
        session.SetUser(user);

        // Assert
        Assert.True(session.IsAuthenticated);
        Assert.Equal(user.Id, session.CurrentUserId);
    }

    [Fact]
    public void Clear_AfterSignIn_RemovesCurrentUser()
    {
        // Arrange
        var session = new UserSession();
        session.SetUser(new UserModel { Id = Guid.NewGuid() });

        // Act
        session.Clear();

        // Assert
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.CurrentUser);
        Assert.Null(session.CurrentUserId);
    }

    [Fact]
    public void SetUser_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var session = new UserSession();

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => session.SetUser(null!));
    }
}
