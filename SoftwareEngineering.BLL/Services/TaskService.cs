using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Exceptions;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Права в проєкті: Owner може все; співвиконавець створює задачі, а видаляє та змінює дедлайн лише у своїх.
/// </summary>
public class TaskService : BaseService, ITaskService
{
    private const int MaxTitleLength = 200;
    private const string TaskCodePrefix = "PL-";

    // Скільки разів пробуємо новий код, якщо його одночасно зайняв хтось інший
    private const int MaxTaskCodeAttempts = 3;

    private const string TaskNotFoundMessage = "Завдання з таким ідентифікатором не знайдено.";
    private const string CategoryNotFoundMessage = "Обрану категорію не знайдено. Оновіть список категорій.";

    private readonly ITaskRepository taskRepository;
    private readonly IProjectRepository projectRepository;
    private readonly ICategoryRepository categoryRepository;

    public TaskService(ITaskRepository taskRepository, IProjectRepository projectRepository, ICategoryRepository categoryRepository)
    {
        this.taskRepository = taskRepository;
        this.projectRepository = projectRepository;
        this.categoryRepository = categoryRepository;
    }

    public Task<Result<IReadOnlyList<TaskModel>>> GetAllAsync()
    {
        return this.ExecuteAsync("Отримання списку всіх завдань", async () =>
        {
            var entities = await this.taskRepository.GetAsync();
            IReadOnlyList<TaskModel> models = entities.Select(ToModel).ToList();
            return Result.Ok(models);
        });
    }

    public Task<Result<IReadOnlyList<TaskModel>>> GetUserTasksAsync(Guid userId)
    {
        return this.ExecuteAsync("Отримання завдань користувача", async () =>
        {
            if (userId == Guid.Empty)
            {
                return Result.Fail<IReadOnlyList<TaskModel>>(ErrorType.Validation, "Ідентифікатор користувача не вказано.");
            }

            var entities = await this.taskRepository.GetByUserAsync(userId);
            IReadOnlyList<TaskModel> models = entities.Select(ToModel).ToList();
            return Result.Ok(models);
        });
    }

    public Task<Result<TaskDetailsModel>> GetDetailsAsync(Guid taskId, Guid currentUserId)
    {
        return this.ExecuteAsync("Перегляд повної інформації про завдання", async () =>
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return Result.Fail<TaskDetailsModel>(ErrorType.Validation, "Не вказано завдання або користувача.");
            }

            var entity = await this.taskRepository.GetDetailsAsync(taskId);
            if (entity is null)
            {
                return Result.Fail<TaskDetailsModel>(ErrorType.NotFound, TaskNotFoundMessage);
            }

            var project = entity.Project;
            var role = project.OwnerId == currentUserId
                ? ProjectRole.Owner
                : project.Members.FirstOrDefault(member => member.UserId == currentUserId)?.Role;
            if (role is null && entity.OwnerId != currentUserId && entity.AssigneeId != currentUserId)
            {
                return Result.Fail<TaskDetailsModel>(ErrorType.Forbidden, "Переглядати завдання можуть лише учасники його проєкту.");
            }

            var members = project.Members
                .OrderBy(member => member.Role)
                .ThenBy(member => member.User.FullName)
                .Select(member => new TaskMemberModel
                {
                    FullName = member.User.FullName,
                    Email = member.User.Email,
                    Role = member.Role,
                })
                .ToList();

            var creator = project.Members.FirstOrDefault(member => member.UserId == entity.CreatorId)?.User;

            return Result.Ok(new TaskDetailsModel
            {
                Task = ToModel(entity),
                CreatedAt = entity.CreatedAt,
                IsOverdue = entity.Status != TaskItemStatus.Done && entity.Deadline.HasValue && entity.Deadline.Value.ToUniversalTime() < DateTime.UtcNow,
                ProjectName = project.Name,
                ProjectOwnerName = project.Owner.FullName,
                OwnerName = entity.Owner.FullName,
                OwnerEmail = entity.Owner.Email,
                CreatorName = creator?.FullName,
                AssigneeName = entity.Assignee?.FullName,
                CategoryName = entity.Category?.Name,
                CategoryColorHex = entity.Category?.ColorHex,
                SubtasksTotal = entity.Subtasks.Count,
                SubtasksCompleted = entity.Subtasks.Count(subtask => subtask.IsCompleted),
                CommentsCount = entity.Comments.Count,
                Members = members,
                CurrentUserRole = role,
                CanEdit = role is ProjectRole.Owner or ProjectRole.CoExecutor || entity.OwnerId == currentUserId,
                CanDeleteOrChangeDeadline = CanManage(entity, currentUserId, role),
            });
        });
    }

    public Task<Result<TaskModel>> CreateAsync(TaskModel model)
    {
        return this.ExecuteAsync("Створення завдання", async () =>
        {
            var validationError = ValidateNewTask(model);
            if (validationError is not null)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, validationError);
            }

            var role = await this.projectRepository.GetMemberRoleAsync(model.ProjectId, model.OwnerId);
            if (role is not (ProjectRole.Owner or ProjectRole.CoExecutor))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Створювати завдання можуть лише власник проєкту та співвиконавці.");
            }

            if (!await this.CategoryExistsAsync(model.CategoryId))
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, CategoryNotFoundMessage);
            }

            var entity = BuildEntity(model);
            await this.AddWithUniqueCodeAsync(entity);

            this.Logger.Information(
                "Користувач {UserId} створив завдання {TaskId} ({TaskCode}) у проєкті {ProjectId}",
                model.OwnerId,
                entity.Id,
                entity.TaskCode,
                entity.ProjectId);

            return Result.Ok(ToModel(entity));
        });
    }

    public Task<Result<TaskModel>> GetByIdAsync(Guid id)
    {
        return this.ExecuteAsync("Перегляд деталей завдання", async () =>
        {
            if (id == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Ідентифікатор завдання не може бути порожнім.");
            }

            var entity = await this.taskRepository.GetByIdAsync(id);
            return entity is null
                ? Result.Fail<TaskModel>(ErrorType.NotFound, TaskNotFoundMessage)
                : Result.Ok(ToModel(entity));
        });
    }

    public Task<Result<TaskModel>> UpdateAsync(TaskModel model, Guid currentUserId)
    {
        return this.ExecuteAsync("Редагування завдання", async () =>
        {
            var validationError = ValidateUpdateTask(model);
            if (validationError is not null)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, validationError);
            }

            if (currentUserId == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Ідентифікатор користувача не вказано.");
            }

            var entity = await this.taskRepository.GetByIdAsync(model.Id);
            if (entity is null)
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, TaskNotFoundMessage);
            }

            var role = await this.projectRepository.GetMemberRoleAsync(entity.ProjectId, currentUserId);
            if (entity.OwnerId != currentUserId && role is not (ProjectRole.Owner or ProjectRole.CoExecutor))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Редагувати завдання можуть лише власник проєкту та співвиконавці.");
            }

            var newDeadline = model.Deadline?.ToUniversalTime();
            if (newDeadline != entity.Deadline && !CanManage(entity, currentUserId, role))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Змінювати дедлайн чужого завдання може лише власник проєкту.");
            }

            if (model.CategoryId != entity.CategoryId && !await this.CategoryExistsAsync(model.CategoryId))
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, CategoryNotFoundMessage);
            }

            entity.Title = model.Title.Trim();
            entity.Description = model.Description;
            entity.Deadline = newDeadline;
            entity.Priority = model.Priority;
            entity.CategoryId = model.CategoryId;

            await this.taskRepository.UpdateAsync(entity);
            this.Logger.Information("Користувач {UserId} відредагував завдання {TaskId}", currentUserId, entity.Id);

            return Result.Ok(ToModel(entity));
        });
    }

    public Task<Result<TaskModel>> DeleteTaskByOwnerAsync(TaskModel model, Guid currentUserId)
    {
        return this.ExecuteAsync("Видалення завдання", async () =>
        {
            if (model is null || model.Id == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Ідентифікатор завдання не вказано.");
            }

            if (currentUserId == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Ідентифікатор користувача не вказано.");
            }

            var entity = await this.taskRepository.GetByIdAsync(model.Id);
            if (entity is null)
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, TaskNotFoundMessage);
            }

            if (!await this.CanManageAsync(entity, currentUserId))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Видаляти можна лише власні завдання (власник проєкту може видаляти будь-які).");
            }

            await this.taskRepository.DeleteAsync(entity);
            this.Logger.Information("Користувач {UserId} видалив завдання {TaskId} ({TaskCode})", currentUserId, entity.Id, entity.TaskCode);

            return Result.Ok(ToModel(entity));
        });
    }

    public Task<Result> DeleteTaskAsync(Guid taskId, Guid currentUserId)
    {
        return this.ExecuteAsync("Видалення спільного завдання", async () =>
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return Result.Fail(ErrorType.Validation, "Не вказано завдання або користувача.");
            }

            var task = await this.taskRepository.GetByIdAsync(taskId);
            if (task is null)
            {
                return Result.Fail(ErrorType.NotFound, "Завдання не знайдено або вже було видалено.");
            }

            if (!await this.CanManageAsync(task, currentUserId))
            {
                return Result.Fail(ErrorType.Forbidden, "У вас немає прав на видалення. Співвиконавець може вилучати лише власні завдання.");
            }

            await this.taskRepository.DeleteAsync(taskId);
            this.Logger.Information("Користувач {UserId} видалив спільне завдання {TaskId}", currentUserId, taskId);

            return Result.Ok();
        });
    }

    public Task<Result<TaskModel>> UpdateDeadlineAsync(Guid taskId, DateTime? newDeadline, Guid currentUserId)
    {
        return this.ExecuteAsync("Редагування дедлайну спільного завдання", async () =>
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Не вказано завдання або користувача.");
            }

            if (!newDeadline.HasValue)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Дедлайн не може бути порожнім.");
            }

            if (newDeadline.Value.ToUniversalTime() <= DateTime.UtcNow)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Дедлайн не може бути в минулому.");
            }

            var entity = await this.taskRepository.GetByIdAsync(taskId);
            if (entity is null)
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, TaskNotFoundMessage);
            }

            // Обмежена дія: дедлайн змінює автор завдання або власник проєкту
            if (!await this.CanManageAsync(entity, currentUserId))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Обмежена дія: лише власник завдання або проєкту має право змінювати дедлайн.");
            }

            var oldDeadline = entity.Deadline;
            entity.Deadline = newDeadline.Value.ToUniversalTime();
            await this.taskRepository.UpdateAsync(entity);

            this.Logger.Information(
                "Користувач {UserId} змінив дедлайн завдання {TaskId} з {OldDeadline} на {NewDeadline}",
                currentUserId,
                taskId,
                oldDeadline,
                entity.Deadline);

            return Result.Ok(ToModel(entity));
        });
    }

    public Task<Result<TaskModel>> UpdateProgressAsync(Guid taskId, TaskItemStatus newStatus, Guid currentUserId)
    {
        return this.ExecuteAsync("Оновлення прогресу завдання", async () =>
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Не вказано завдання або користувача.");
            }

            if (!Enum.IsDefined(newStatus))
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Невідомий статус завдання.");
            }

            var entity = await this.taskRepository.GetByIdAsync(taskId);
            if (entity is null)
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, TaskNotFoundMessage);
            }

            // Статус, як і інші поля, змінюють учасники проєкту; призначений виконавець — також
            var role = await this.projectRepository.GetMemberRoleAsync(entity.ProjectId, currentUserId);
            if (entity.AssigneeId != currentUserId && entity.OwnerId != currentUserId && role is not (ProjectRole.Owner or ProjectRole.CoExecutor))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Оновлювати прогрес можуть лише учасники проєкту.");
            }

            if (entity.Status == newStatus)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Завдання вже має такий статус.");
            }

            var oldStatus = entity.Status;
            entity.Status = newStatus;
            await this.taskRepository.UpdateAsync(entity);

            this.Logger.Information(
                "Користувач {UserId} змінив статус завдання {TaskId} з {OldStatus} на {NewStatus}",
                currentUserId,
                taskId,
                oldStatus,
                newStatus);

            return Result.Ok(ToModel(entity));
        });
    }

    public Task<Result<TaskModel>> AssignAsync(Guid taskId, Guid? assigneeId, Guid currentUserId)
    {
        return this.ExecuteAsync("Призначення виконавця", async () =>
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Не вказано завдання або користувача.");
            }

            if (assigneeId == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Некоректний ідентифікатор виконавця.");
            }

            var entity = await this.taskRepository.GetByIdAsync(taskId);
            if (entity is null)
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, TaskNotFoundMessage);
            }

            var role = await this.projectRepository.GetMemberRoleAsync(entity.ProjectId, currentUserId);
            if (role is not (ProjectRole.Owner or ProjectRole.CoExecutor))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Призначати виконавця можуть лише власник проєкту та співвиконавці.");
            }

            if (assigneeId.HasValue && await this.projectRepository.GetMemberRoleAsync(entity.ProjectId, assigneeId.Value) is null)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Вибраний користувач не є учасником цього проєкту.");
            }

            if (entity.AssigneeId == assigneeId)
            {
                return Result.Ok(ToModel(entity));
            }

            entity.AssigneeId = assigneeId;
            await this.taskRepository.UpdateAsync(entity);

            if (assigneeId.HasValue)
            {
                this.Logger.Information("Користувача {AssigneeId} призначено на {TaskId}", assigneeId.Value, taskId);
            }
            else
            {
                this.Logger.Information("Виконавця знято із завдання {TaskId}", taskId);
            }

            return Result.Ok(ToModel(entity));
        });
    }

    public Task<Result<IReadOnlyList<AssigneeOptionModel>>> GetAssigneeCandidatesAsync(Guid taskId, Guid currentUserId)
    {
        return this.ExecuteAsync("Отримання кандидатів у виконавці", async () =>
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return Result.Fail<IReadOnlyList<AssigneeOptionModel>>(ErrorType.Validation, "Не вказано завдання або користувача.");
            }

            var entity = await this.taskRepository.GetByIdAsync(taskId);
            if (entity is null)
            {
                return Result.Fail<IReadOnlyList<AssigneeOptionModel>>(ErrorType.NotFound, TaskNotFoundMessage);
            }

            var role = await this.projectRepository.GetMemberRoleAsync(entity.ProjectId, currentUserId);
            if (role is not (ProjectRole.Owner or ProjectRole.CoExecutor))
            {
                return Result.Fail<IReadOnlyList<AssigneeOptionModel>>(
                    ErrorType.Forbidden,
                    "Призначати виконавця можуть лише власник проєкту та співвиконавці.");
            }

            var users = await this.projectRepository.GetProjectUsersAsync(entity.ProjectId);
            IReadOnlyList<AssigneeOptionModel> options = users
                .Select(user => new AssigneeOptionModel { UserId = user.Id, FullName = user.FullName, Email = user.Email })
                .ToList();

            return Result.Ok(options);
        });
    }

    public Task<Result<TaskModel>> DuplicateAsync(Guid sourceTaskId, DuplicateTaskModel model, Guid currentUserId)
    {
        return this.ExecuteAsync("Копіювання завдання", async () =>
        {
            if (sourceTaskId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Не вказано завдання або користувача.");
            }

            if (model is null)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Дані для копії не передано.");
            }

            var title = model.Title?.Trim() ?? string.Empty;
            if (title.Length == 0)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Назва копії не може бути порожньою.");
            }

            if (title.Length > MaxTitleLength)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Назва занадто довга.");
            }

            if (!model.Deadline.HasValue)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Дедлайн не вказано.");
            }

            var deadline = model.Deadline.Value.ToUniversalTime();
            if (deadline <= DateTime.UtcNow)
            {
                return Result.Fail<TaskModel>(ErrorType.Validation, "Дедлайн не може бути в минулому.");
            }

            // Деталі потрібні заради підзадач; категорію з ON DELETE SET NULL отримуємо вже актуальну
            var source = await this.taskRepository.GetDetailsAsync(sourceTaskId);
            if (source is null)
            {
                return Result.Fail<TaskModel>(ErrorType.NotFound, "Завдання, яке ви копіюєте, не знайдено. Можливо, його вже видалили.");
            }

            // Права ті самі, що на створення: копія — це нове завдання в тому ж проєкті
            var role = await this.projectRepository.GetMemberRoleAsync(source.ProjectId, currentUserId);
            if (role is not (ProjectRole.Owner or ProjectRole.CoExecutor))
            {
                return Result.Fail<TaskModel>(ErrorType.Forbidden, "Копіювати завдання можуть лише власник проєкту та співвиконавці.");
            }

            var copy = BuildEntity(new TaskModel
            {
                ProjectId = source.ProjectId,
                OwnerId = currentUserId,
                Title = title,
                Description = source.Description,
                Deadline = deadline,
                Priority = source.Priority,
                CategoryId = source.CategoryId,
            });

            if (model.CopySubtasks)
            {
                foreach (var subtask in source.Subtasks)
                {
                    copy.Subtasks.Add(new Subtask
                    {
                        Id = Guid.NewGuid(),
                        TaskId = copy.Id,
                        Title = subtask.Title,
                        IsCompleted = false,
                    });
                }
            }

            // Завдання і його підзадачі записуються одним SaveChanges — або все, або нічого
            await this.AddWithUniqueCodeAsync(copy);

            this.Logger.Information(
                "Користувач {UserId} скопіював завдання {SourceTaskId} → {NewTaskId} ({TaskCode}), підзадач: {SubtaskCount}",
                currentUserId,
                sourceTaskId,
                copy.Id,
                copy.TaskCode,
                copy.Subtasks.Count);

            return Result.Ok(ToModel(copy));
        });
    }

    /// <summary>
    /// Наступний код = найбільший наявний номер + 1. Кількість завдань не підходить:
    /// після видалення (PL-1, PL-3 лишились) вона дала б уже зайнятий PL-3.
    /// Номери видалених завдань повторно не видаються — як у Jira.
    /// </summary>
    public static string NextTaskCode(IEnumerable<string> existingCodes)
    {
        var maxNumber = existingCodes
            .Where(code => code.StartsWith(TaskCodePrefix, StringComparison.Ordinal))
            .Select(code => int.TryParse(code.AsSpan(TaskCodePrefix.Length), out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{TaskCodePrefix}{maxNumber + 1}";
    }

    private static bool CanManage(TaskItem task, Guid userId, ProjectRole? role)
    {
        return task.OwnerId == userId || role == ProjectRole.Owner;
    }

    /// <summary>
    /// Видаляти завдання та змінювати дедлайн може його власник або власник проєкту.
    /// </summary>
    private async Task<bool> CanManageAsync(TaskItem task, Guid userId)
    {
        if (task.OwnerId == userId)
        {
            return true;
        }

        var role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, userId);
        return CanManage(task, userId, role);
    }

    /// <summary>
    /// Категорію могли видалити, поки форма була відкрита, — перевіряємо до запису в БД.
    /// </summary>
    private async Task<bool> CategoryExistsAsync(int? categoryId)
    {
        return !categoryId.HasValue || await this.categoryRepository.GetByIdAsync(categoryId.Value) is not null;
    }

    private static string? ValidateNewTask(TaskModel? model)
    {
        if (model is null)
        {
            return "Дані завдання не передано.";
        }

        return ValidateTitle(model.Title)
            ?? (model.ProjectId == Guid.Empty ? "Не вказано проєкт." : null)
            ?? (model.OwnerId == Guid.Empty ? "Не вказано власника завдання." : null)
            ?? (!model.Deadline.HasValue ? "Дедлайн не вказано." : null)
            ?? ValidateFutureDeadline(model.Deadline);
    }

    private static string? ValidateUpdateTask(TaskModel? model)
    {
        if (model is null)
        {
            return "Дані завдання не передано.";
        }

        return (model.Id == Guid.Empty ? "Ідентифікатор завдання не вказано." : null)
            ?? ValidateTitle(model.Title)
            ?? ValidateFutureDeadline(model.Deadline);
    }

    private static string? ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Назва завдання не може бути порожньою.";
        }

        return title.Trim().Length > MaxTitleLength ? "Назва занадто довга." : null;
    }

    private static string? ValidateFutureDeadline(DateTime? deadline)
    {
        return deadline.HasValue && deadline.Value.ToUniversalTime() <= DateTime.UtcNow
            ? "Дедлайн не може бути в минулому."
            : null;
    }

    /// <summary>
    /// Генерує код і зберігає завдання. Якщо код щойно зайняв інший користувач, бере наступний.
    /// Тут try/catch — не обробка помилки, а повторна спроба: після останньої спроби виняток іде в ExecuteAsync.
    /// </summary>
    private async Task AddWithUniqueCodeAsync(TaskItem entity)
    {
        for (var attempt = 1; ; attempt++)
        {
            entity.TaskCode = NextTaskCode(await this.taskRepository.GetTaskCodesAsync(entity.ProjectId));

            try
            {
                await this.taskRepository.AddAsync(entity);
                return;
            }
            catch (DuplicateTaskCodeException ex) when (attempt < MaxTaskCodeAttempts)
            {
                this.Logger.Warning(
                    "Код {TaskCode} у проєкті {ProjectId} щойно зайняли, пробуємо наступний (спроба {Attempt} з {MaxAttempts})",
                    ex.TaskCode,
                    entity.ProjectId,
                    attempt,
                    MaxTaskCodeAttempts);
            }
        }
    }

    private static TaskItem BuildEntity(TaskModel model)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = model.ProjectId,
            OwnerId = model.OwnerId,
            CreatorId = model.OwnerId,
            Title = model.Title.Trim(),
            Description = model.Description,
            Deadline = model.Deadline!.Value.ToUniversalTime(),
            Priority = model.Priority,
            CategoryId = model.CategoryId,
            Status = TaskItemStatus.Todo,
        };
    }

    private static TaskModel ToModel(TaskItem entity)
    {
        return new TaskModel
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            TaskCode = entity.TaskCode,
            Title = entity.Title,
            Description = entity.Description,
            Deadline = entity.Deadline,
            Priority = entity.Priority,
            CategoryId = entity.CategoryId,
            CategoryName = entity.Category?.Name,
            CategoryColorHex = entity.Category?.ColorHex,
            Status = entity.Status,
            OwnerId = entity.OwnerId,
            AssigneeId = entity.AssigneeId,
        };
    }
}
