using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class CommentService : BaseService, ICommentService
{
    public const int MaxContentLength = 1000;

    private const string UnexpectedErrorMessage = "Сталася внутрішня помилка. Спробуйте ще раз.";

    private readonly ICommentRepository commentRepository;
    private readonly ITaskRepository taskRepository;
    private readonly IProjectRepository projectRepository;

    public CommentService(ICommentRepository commentRepository, ITaskRepository taskRepository, IProjectRepository projectRepository)
    {
        this.commentRepository = commentRepository;
        this.taskRepository = taskRepository;
        this.projectRepository = projectRepository;
    }

    public async Task<Result<IReadOnlyList<CommentModel>>> GetByTaskAsync(Guid taskId, Guid currentUserId)
    {
        try
        {
            var access = await this.GetAccessAsync(taskId, currentUserId);
            if (access.Error is { } error)
            {
                return this.Fail<IReadOnlyList<CommentModel>>(error.Type, error.Message);
            }

            if (!access.CanParticipate)
            {
                return this.Fail<IReadOnlyList<CommentModel>>(ErrorType.Forbidden, "Переглядати коментарі можуть лише учасники проєкту.");
            }

            var comments = await this.commentRepository.GetByTaskIdAsync(taskId);

            // Роль кожного автора в проєкті — щоб показати «Ім'я (Роль)»; запит на автора, а не на коментар
            var roles = new Dictionary<Guid, ProjectRole?>();
            foreach (var authorId in comments.Select(comment => comment.AuthorId).Distinct())
            {
                roles[authorId] = await this.projectRepository.GetMemberRoleAsync(access.ProjectId, authorId);
            }

            IReadOnlyList<CommentModel> models = comments
                .Select(comment => ToModel(comment, comment.Author?.FullName, roles[comment.AuthorId], currentUserId, access.IsProjectOwner))
                .ToList();

            return Result.Ok(models);
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час отримання коментарів завдання {TaskId}", taskId);
            return Result.Fail<IReadOnlyList<CommentModel>>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    public async Task<Result<CommentModel>> AddAsync(Guid taskId, string content, Guid currentUserId)
    {
        try
        {
            this.Logger.Information("Спроба додати коментар до завдання {TaskId} користувачем {UserId}", taskId, currentUserId);

            var trimmedContent = content?.Trim() ?? string.Empty;
            if (trimmedContent.Length == 0)
            {
                return this.Fail<CommentModel>(ErrorType.Validation, "Коментар не може бути порожнім.");
            }

            if (trimmedContent.Length > MaxContentLength)
            {
                return this.Fail<CommentModel>(ErrorType.Validation, $"Коментар не може перевищувати {MaxContentLength} символів.");
            }

            var access = await this.GetAccessAsync(taskId, currentUserId);
            if (access.Error is { } error)
            {
                return this.Fail<CommentModel>(error.Type, error.Message);
            }

            if (!access.CanParticipate)
            {
                return this.Fail<CommentModel>(ErrorType.Forbidden, "Коментувати завдання можуть лише учасники проєкту.");
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                AuthorId = currentUserId,
                Content = trimmedContent,
                CreatedAt = DateTime.UtcNow,
            };

            await this.commentRepository.AddAsync(comment);
            this.Logger.Information("Користувач {UserId} прокоментував завдання {TaskId} (коментар {CommentId})", currentUserId, taskId, comment.Id);

            return Result.Ok(ToModel(comment, null, access.Role, currentUserId, access.IsProjectOwner));
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час додавання коментаря до завдання {TaskId}", taskId);
            return Result.Fail<CommentModel>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    public async Task<Result> DeleteAsync(Guid commentId, Guid currentUserId)
    {
        try
        {
            if (commentId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return this.Fail(ErrorType.Validation, "Не вказано коментар або користувача.");
            }

            var comment = await this.commentRepository.GetByIdAsync(commentId);
            if (comment is null)
            {
                return this.Fail(ErrorType.NotFound, "Коментар не знайдено. Можливо, його вже видалили.");
            }

            var access = await this.GetAccessAsync(comment.TaskId, currentUserId);
            if (access.Error is { } error)
            {
                return this.Fail(error.Type, error.Message);
            }

            if (comment.AuthorId != currentUserId && !access.IsProjectOwner)
            {
                return this.Fail(ErrorType.Forbidden, "Видалити коментар може лише його автор або власник проєкту.");
            }

            await this.commentRepository.DeleteAsync(commentId);
            this.Logger.Information("Користувач {UserId} видалив коментар {CommentId} завдання {TaskId}", currentUserId, commentId, comment.TaskId);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час видалення коментаря {CommentId}", commentId);
            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    private static CommentModel ToModel(Comment comment, string? authorName, ProjectRole? authorRole, Guid currentUserId, bool isProjectOwner)
    {
        return new CommentModel
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            AuthorId = comment.AuthorId,
            AuthorName = authorName ?? string.Empty,
            AuthorRole = authorRole,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            CanDelete = comment.AuthorId == currentUserId || isProjectOwner,
        };
    }

    /// <summary>
    /// Хто бере участь в обговоренні: ті самі правила доступу до завдання, що й для підзадач.
    /// </summary>
    private async Task<TaskAccess> GetAccessAsync(Guid taskId, Guid userId)
    {
        if (taskId == Guid.Empty || userId == Guid.Empty)
        {
            return TaskAccess.Failed(ErrorType.Validation, "Не вказано завдання або користувача.");
        }

        var task = await this.taskRepository.GetByIdAsync(taskId);
        if (task is null)
        {
            return TaskAccess.Failed(ErrorType.NotFound, "Завдання не знайдено або його вже видалено.");
        }

        var role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, userId);
        var canParticipate = role != null || task.OwnerId == userId || task.AssigneeId == userId;

        return new TaskAccess(task.ProjectId, role, canParticipate, role == ProjectRole.Owner, null);
    }

    private Result<T> Fail<T>(ErrorType type, string message)
    {
        this.Logger.Warning("Дію з коментарем відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail<T>(type, message);
    }

    private Result Fail(ErrorType type, string message)
    {
        this.Logger.Warning("Дію з коментарем відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail(type, message);
    }

    private sealed record TaskAccess(Guid ProjectId, ProjectRole? Role, bool CanParticipate, bool IsProjectOwner, Error? Error)
    {
        public static TaskAccess Failed(ErrorType type, string message) => new(Guid.Empty, null, false, false, new Error(type, message));
    }
}
