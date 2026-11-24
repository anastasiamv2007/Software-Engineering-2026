using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class ReviewService : BaseService, IReviewService
{
    private readonly ITaskRepository taskRepository;
    private readonly IProjectRepository projectRepository;
    private readonly ICommentRepository commentRepository;

    public ReviewService(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository,
        ICommentRepository commentRepository)
    {
        this.taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        this.projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
        this.commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
    }

    public async Task<Result> SubmitForReviewAsync(Guid taskId, Guid currentUserId)
    {
        if (taskId == Guid.Empty)
        {
            return this.Fail(ErrorType.Validation, "Некоректний ідентифікатор завдання.");
        }

        if (currentUserId == Guid.Empty)
        {
            return this.Fail(ErrorType.Validation, "Некоректний ідентифікатор користувача.");
        }

        var task = await this.taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            return this.Fail(ErrorType.NotFound, "Завдання не знайдено.");
        }

        var role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, currentUserId);
        var canSubmit = role is ProjectRole.Owner or ProjectRole.CoExecutor || task.AssigneeId == currentUserId;
        if (!canSubmit)
        {
            return this.Fail(ErrorType.Forbidden, "Лише співвиконавець або власник проєкту може відправити завдання на перевірку.");
        }

        if (task.Status is not (TaskItemStatus.Todo or TaskItemStatus.InProgress or TaskItemStatus.NeedsRevision))
        {
            return this.Fail(
                ErrorType.Validation,
                "На перевірку можна відправити лише завдання зі статусом «До виконання», «В процесі» або «Потребує доопрацювання».");
        }

        var oldStatus = task.Status;
        task.Status = TaskItemStatus.InReview;
        await this.taskRepository.UpdateAsync(task);

        this.Logger.Information(
            "Завдання {TaskId} відправлено на перевірку користувачем {UserId}: {OldStatus} → {NewStatus}",
            taskId,
            currentUserId,
            oldStatus,
            task.Status);

        return Result.Ok();
    }

    public async Task<Result> ApproveAsync(Guid taskId, Guid currentUserId)
    {
        if (taskId == Guid.Empty)
        {
            return this.Fail(ErrorType.Validation, "Некоректний ідентифікатор завдання.");
        }

        if (currentUserId == Guid.Empty)
        {
            return this.Fail(ErrorType.Validation, "Некоректний ідентифікатор користувача.");
        }

        var task = await this.taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            return this.Fail(ErrorType.NotFound, "Завдання не знайдено.");
        }

        var role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, currentUserId);
        if (role is not (ProjectRole.Owner or ProjectRole.Reviewer))
        {
            return this.Fail(ErrorType.Forbidden, "Лише власник проєкту або рецензент може прийняти завдання.");
        }

        if (task.Status != TaskItemStatus.InReview)
        {
            return this.Fail(ErrorType.Validation, "Прийняти можна лише завдання, яке перебуває на перевірці.");
        }

        task.Status = TaskItemStatus.Done;
        await this.taskRepository.UpdateAsync(task);

        this.Logger.Information(
            "Завдання {TaskId} прийнято рецензентом {UserId} (роль {Role}): InReview → Done",
            taskId,
            currentUserId,
            role);

        return Result.Ok();
    }

    public async Task<Result> RequestRevisionAsync(Guid taskId, string comment, Guid currentUserId)
    {
        if (taskId == Guid.Empty)
        {
            return this.Fail(ErrorType.Validation, "Некоректний ідентифікатор завдання.");
        }

        if (currentUserId == Guid.Empty)
        {
            return this.Fail(ErrorType.Validation, "Некоректний ідентифікатор користувача.");
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            return this.Fail(ErrorType.Validation, "Вкажіть коментар для доопрацювання.");
        }

        var trimmedComment = comment.Trim();
        if (trimmedComment.Length > 1000)
        {
            return this.Fail(ErrorType.Validation, "Коментар не може перевищувати 1000 символів.");
        }

        var task = await this.taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            return this.Fail(ErrorType.NotFound, "Завдання не знайдено.");
        }

        var role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, currentUserId);
        if (role is not (ProjectRole.Owner or ProjectRole.Reviewer))
        {
            return this.Fail(ErrorType.Forbidden, "Лише власник проєкту або рецензент може повернути завдання на доопрацювання.");
        }

        if (task.Status != TaskItemStatus.InReview)
        {
            return this.Fail(ErrorType.Validation, "Повернути на доопрацювання можна лише завдання, яке перебуває на перевірці.");
        }

        task.Status = TaskItemStatus.NeedsRevision;
        await this.taskRepository.UpdateAsync(task);

        await this.commentRepository.AddAsync(new Comment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorId = currentUserId,
            Content = $"[Повернуто на доопрацювання]: {trimmedComment}",
            CreatedAt = DateTime.UtcNow,
        });

        this.Logger.Information(
            "Завдання {TaskId} повернуто на доопрацювання користувачем {UserId} (роль {Role}): InReview → NeedsRevision",
            taskId,
            currentUserId,
            role);

        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<ReviewTaskItemModel>>> GetReviewQueueAsync(Guid currentUserId)
    {
        if (currentUserId == Guid.Empty)
        {
            return this.Fail<IReadOnlyList<ReviewTaskItemModel>>(ErrorType.Validation, "Некоректний ідентифікатор користувача.");
        }

        var userTasks = await this.taskRepository.GetByUserAsync(currentUserId);
        var inReviewTasks = userTasks
            .Where(t => t.Status == TaskItemStatus.InReview)
            .ToList();

        var roleByProject = new Dictionary<Guid, ProjectRole?>();
        var queue = new List<ReviewTaskItemModel>();

        foreach (var task in inReviewTasks)
        {
            if (!roleByProject.TryGetValue(task.ProjectId, out var role))
            {
                role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, currentUserId);
                roleByProject[task.ProjectId] = role;
            }

            if (role is ProjectRole.Owner or ProjectRole.Reviewer)
            {
                queue.Add(new ReviewTaskItemModel
                {
                    TaskId = task.Id,
                    ProjectId = task.ProjectId,
                    TaskCode = task.TaskCode,
                    Title = task.Title,
                    Description = task.Description,
                    CategoryName = task.Category?.Name,
                    CategoryColorHex = task.Category?.ColorHex,
                    Priority = task.Priority,
                    Deadline = task.Deadline,
                    ReviewerRole = role.Value,
                });
            }
        }

        IReadOnlyList<ReviewTaskItemModel> ordered = queue
            .OrderBy(item => item.Deadline ?? DateTime.MaxValue)
            .ToList();

        return Result.Ok(ordered);
    }
}