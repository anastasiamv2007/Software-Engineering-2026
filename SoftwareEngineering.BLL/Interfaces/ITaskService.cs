using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Interfaces;

public interface ITaskService : IService
{
    Task<Result<IReadOnlyList<TaskModel>>> GetAllAsync();

    /// <summary>
    /// Завдання лише тих проєктів, де користувач є власником або співвиконавцем.
    /// </summary>
    Task<Result<IReadOnlyList<TaskModel>>> GetUserTasksAsync(Guid userId);

    /// <summary>
    /// Повна інформація про завдання; доступна лише учасникам його проєкту.
    /// </summary>
    Task<Result<TaskDetailsModel>> GetDetailsAsync(Guid taskId, Guid currentUserId);

    Task<Result<TaskModel>> CreateAsync(TaskModel model);

    Task<Result<TaskModel>> GetByIdAsync(Guid id);

    Task<Result<TaskModel>> UpdateAsync(TaskModel model, Guid currentUserId);

    Task<Result<TaskModel>> UpdateProgressAsync(Guid taskId, TaskItemStatus newStatus, Guid currentUserId);

    Task<Result<TaskModel>> UpdateDeadlineAsync(Guid taskId, DateTime? newDeadline, Guid currentUserId);

    Task<Result<TaskModel>> DeleteTaskByOwnerAsync(TaskModel model, Guid currentUserId);

    Task<Result> DeleteTaskAsync(Guid taskId, Guid currentUserId);

    Task<Result<TaskModel>> AssignAsync(Guid taskId, Guid? assigneeId, Guid currentUserId);

    Task<Result<IReadOnlyList<AssigneeOptionModel>>> GetAssigneeCandidatesAsync(Guid taskId, Guid currentUserId);

    /// <summary>
    /// Створює копію завдання в тому ж проєкті: новий код, статус «До виконання», власник — поточний користувач.
    /// Коментарі, виконавець і статус не копіюються; підзадачі — за бажанням і без відміток.
    /// </summary>
    Task<Result<TaskModel>> DuplicateAsync(Guid sourceTaskId, DuplicateTaskModel model, Guid currentUserId);
}
