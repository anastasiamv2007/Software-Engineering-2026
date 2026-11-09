using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;
using System;
using System.Threading.Tasks;
using Xunit;

namespace SoftwareEngineering.Tests.Services;

public class CommentServiceTests
{
    private readonly Mock<ICommentRepository> _commentRepoMock;
    private readonly Mock<ITaskRepository> _taskRepoMock;
    private readonly Mock<IProjectRepository> _projectRepoMock;
    private readonly CommentService _commentService;

    public CommentServiceTests()
    {
        _commentRepoMock = new Mock<ICommentRepository>();
        _taskRepoMock = new Mock<ITaskRepository>();
        _projectRepoMock = new Mock<IProjectRepository>();

        _commentService = new CommentService(
            _commentRepoMock.Object,
            _taskRepoMock.Object,
            _projectRepoMock.Object);
    }

    // 1. Тест перевіряє успішне додавання коментаря, коли задача існує 
    // і користувач є повноправним учасником проєкту.
    [Fact]
    public async Task AddCommentAsync_ValidData_ReturnsSuccess()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var content = "Це тестовий коментар";

        _taskRepoMock.Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync(new TaskItem { Id = taskId, ProjectId = projectId });

        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, userId))
            .ReturnsAsync(ProjectRole.CoExecutor);

        // Act
        var result = await _commentService.AddCommentAsync(taskId, userId, content);

        // Assert
        Assert.True(result.IsSuccess);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>()), Times.Once);
    }

    // 2. Тест перевіряє валідацію тексту: якщо коментар порожній або складається 
    // лише з пробілів, сервіс повинен повернути помилку валідації і не викликати репозиторій.
    [Fact]
    public async Task AddCommentAsync_EmptyContent_ReturnsValidationFailure()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var result = await _commentService.AddCommentAsync(taskId, userId, "   ");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>()), Times.Never);
    }

    // 3. Тест перевіряє права доступу: якщо користувач не є учасником проєкту, 
    // до якого належить задача, сервіс повинен повернути статус Forbidden (доступ заборонено).
    [Fact]
    public async Task AddCommentAsync_UserNotMember_ReturnsForbiddenFailure()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var content = "Коментар від стороннього";

        _taskRepoMock.Setup(r => r.GetByIdAsync(taskId))
            .ReturnsAsync(new TaskItem { Id = taskId, ProjectId = projectId });

        // Користувач не є учасником проєкту (репозиторій повертає null)
        _projectRepoMock.Setup(r => r.GetMemberRoleAsync(projectId, userId))
            .ReturnsAsync((ProjectRole?)null);

        // Act
        var result = await _commentService.AddCommentAsync(taskId, userId, content);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error?.Type);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>()), Times.Never);
    }
}