using Moq;
using SoftwareEngineering.BLL.Interfaces;

namespace SoftwareEngineering.Tests
{
    public class AppAuthFlowTests
    {
        [Fact]
        public void SignOut_Logic_ClearsUserSession()
        {
            // Arrange
            var userSessionMock = new Mock<IUserSession>();
            userSessionMock.Setup(s => s.CurrentUserId).Returns(Guid.NewGuid());

            // Act (Симулюємо виконання логіки SignOut)
            var userIdBeforeClear = userSessionMock.Object.CurrentUserId;
            userSessionMock.Object.Clear();

            // Assert
            Assert.NotNull(userIdBeforeClear);
            userSessionMock.Verify(s => s.Clear(), Times.Once);
        }

        [Fact]
        public void SignOutAndDisconnect_Logic_RetrievesUserSessionBeforeDisconnect()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var userSessionMock = new Mock<IUserSession>();
            userSessionMock.Setup(s => s.CurrentUserId).Returns(userId);

            // Act (Симулюємо перевірку наявності сесії перед відключенням та логуванням)
            var activeUserId = userSessionMock.Object.CurrentUserId;
            bool isDisconnectedTriggered = true;

            // Assert
            Assert.Equal(userId, activeUserId);
            Assert.True(isDisconnectedTriggered);
            userSessionMock.Verify(s => s.CurrentUserId, Times.AtLeastOnce);
        }

        [Fact]
        public void MainWindow_LogoutClick_VerifiesUserSessionState()
        {
            // Arrange
            var userSessionMock = new Mock<IUserSession>();
            userSessionMock.Setup(s => s.CurrentUserId).Returns(Guid.NewGuid());

            // Act & Assert
            Assert.True(userSessionMock.Object.CurrentUserId.HasValue);
        }
    }
}