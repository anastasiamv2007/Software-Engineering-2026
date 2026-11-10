using Moq;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class CommentServiceTests
{
    private readonly Mock<ICommentRepository> commentRepositoryMock = new();
    private readonly Mock<ITaskRepository> taskRepositoryMock = new();
    private readonly Mock<IProjectRepository> projectRepositoryMock = new();

    private readonly Guid userId = Guid.NewGuid();

    private readonly TaskItem task = new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
        Title = "Лабораторна",
        TaskCode = "PL-1",
    };

    public CommentServiceTests()
    {
        this.taskRepositoryMock.Setup(repository => repository.GetByIdAsync(this.task.Id)).ReturnsAsync(this.task);

        // За замовчуванням користувач — співвиконавець проєкту
        this.SetRole(this.userId, ProjectRole.CoExecutor);
    }

    // 1. Учасник проєкту успішно додає коментар
    [Fact]
    public async Task AddCommentAsync_ValidData_ReturnsSuccess()
    {
        // Arrange
        Comment? saved = null;
        this.commentRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Comment>()))
            .Callback<Comment>(comment => saved = comment)
            .Returns(Task.CompletedTask);
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, "  Це тестовий коментар  ", this.userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Це тестовий коментар", saved!.Content);
        Assert.Equal(this.userId, saved.AuthorId);
        Assert.NotEqual(default, saved.CreatedAt);
        this.commentRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Comment>()), Times.Once);
    }

    // 2. Порожній коментар або лише пробіли — помилка валідації, репозиторій не викликається
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddCommentAsync_EmptyContent_ReturnsValidationFailure(string content)
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, content, this.userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        this.commentRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Comment>()), Times.Never);
    }

    // 3. Сторонній користувач не може коментувати
    [Fact]
    public async Task AddCommentAsync_UserNotMember_ReturnsForbiddenFailure()
    {
        // Arrange
        this.SetRole(this.userId, null);
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, "Коментар від стороннього", this.userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error?.Type);
        this.commentRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Comment>()), Times.Never);
    }

    [Fact]
    public async Task AddCommentAsync_TooLong_ReturnsValidationFailure()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, new string('a', CommentService.MaxContentLength + 1), this.userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
    }

    [Fact]
    public async Task AddCommentAsync_TaskNotFound_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(Guid.NewGuid(), "Текст", this.userId);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task AddCommentAsync_AssigneeOutsideProject_IsAllowed()
    {
        // Arrange
        this.SetRole(this.userId, null);
        this.task.AssigneeId = this.userId;
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, "Готово до перевірки", this.userId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AddCommentAsync_RepositoryThrows_ReturnsUnexpectedWithoutTechnicalDetails()
    {
        // Arrange
        this.commentRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Comment>()))
            .ThrowsAsync(new InvalidOperationException("connection refused"));
        var service = this.CreateService();

        // Act
        var result = await service.AddAsync(this.task.Id, "Текст", this.userId);

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error?.Type);
        Assert.DoesNotContain("connection refused", result.Error!.Message);
    }

    [Fact]
    public async Task GetByTaskAsync_Member_ReturnsCommentsWithAuthorNameRoleAndDeleteRights()
    {
        // Arrange
        var otherAuthor = new User { Id = Guid.NewGuid(), FullName = "Турчин Павло", Email = "pavlo@test.com" };
        var me = new User { Id = this.userId, FullName = "Ihor Lihvan", Email = "ihor@test.com" };
        this.SetRole(otherAuthor.Id, ProjectRole.Owner);
        this.commentRepositoryMock
            .Setup(repository => repository.GetByTaskIdAsync(this.task.Id))
            .ReturnsAsync(new List<Comment>
            {
                new() { Id = Guid.NewGuid(), TaskId = this.task.Id, AuthorId = otherAuthor.Id, Author = otherAuthor, Content = "Перший" },
                new() { Id = Guid.NewGuid(), TaskId = this.task.Id, AuthorId = me.Id, Author = me, Content = "Другий" },
            });
        var service = this.CreateService();

        // Act
        var result = await service.GetByTaskAsync(this.task.Id, this.userId);

        // Assert
        var comments = result.Value!;
        Assert.Equal(2, comments.Count);
        Assert.Equal("Турчин Павло", comments[0].AuthorName);
        Assert.Equal(ProjectRole.Owner, comments[0].AuthorRole);
        Assert.False(comments[0].CanDelete);
        Assert.True(comments[1].CanDelete);
    }

    [Fact]
    public async Task GetByTaskAsync_UserNotMember_ReturnsForbidden()
    {
        // Arrange
        this.SetRole(this.userId, null);
        var service = this.CreateService();

        // Act
        var result = await service.GetByTaskAsync(this.task.Id, this.userId);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error?.Type);
        this.commentRepositoryMock.Verify(repository => repository.GetByTaskIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Author_DeletesOwnComment()
    {
        // Arrange
        var comment = this.SetupComment(this.userId);
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(comment.Id, this.userId);

        // Assert
        Assert.True(result.IsSuccess);
        this.commentRepositoryMock.Verify(repository => repository.DeleteAsync(comment.Id), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_CoExecutorDeletesForeignComment_ReturnsForbidden()
    {
        // Arrange
        var comment = this.SetupComment(Guid.NewGuid());
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(comment.Id, this.userId);

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error?.Type);
        this.commentRepositoryMock.Verify(repository => repository.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ProjectOwnerDeletesForeignComment_Deletes()
    {
        // Arrange
        this.SetRole(this.userId, ProjectRole.Owner);
        var comment = this.SetupComment(Guid.NewGuid());
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(comment.Id, this.userId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteAsync_CommentNotFound_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid(), this.userId);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error?.Type);
    }

    private Comment SetupComment(Guid authorId)
    {
        var comment = new Comment { Id = Guid.NewGuid(), TaskId = this.task.Id, AuthorId = authorId, Content = "Текст" };
        this.commentRepositoryMock.Setup(repository => repository.GetByIdAsync(comment.Id)).ReturnsAsync(comment);
        return comment;
    }

    private void SetRole(Guid memberId, ProjectRole? role)
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(this.task.ProjectId, memberId))
            .ReturnsAsync(role);
    }

    private CommentService CreateService()
    {
        return new CommentService(this.commentRepositoryMock.Object, this.taskRepositoryMock.Object, this.projectRepositoryMock.Object);
    }
}
