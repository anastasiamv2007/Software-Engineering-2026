using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Interfaces;

public interface ITaskService : IService
{
    Task<IReadOnlyList<TaskModel>> GetAllAsync();

    /// <summary>
    /// Завдання лише тих проєктів, де користувач є власником або співвиконавцем.
    /// </summary>
    Task<IReadOnlyList<TaskModel>> GetUserTasksAsync(Guid userId);

    /// <summary>
    /// Повна інформація про завдання; доступна лише учасникам його проєкту.
    /// </summary>
    Task<TaskDetailsModel> GetDetailsAsync(Guid taskId, Guid currentUserId);

    Task<TaskModel> CreateAsync(TaskModel model);


    // 7 update progress
    Task<TaskModel> UpdateProgressAsync(Guid taskId, TaskItemStatus newStatus, Guid currentUserId);

    // 2
    Task<TaskModel> GetByIdAsync(Guid id);

    //3
    Task<TaskModel> UpdateAsync(TaskModel model, Guid currentUserId);

    // 4 delete task
    Task<TaskModel> DeleteTaskByOwnerAsync(TaskModel model, Guid currentUserId);

    // 5

    Task DeleteTaskAsync(Guid taskId, Guid currentUserId);

    // 6
    Task<TaskModel> UpdateDeadlineAsync(Guid taskId, DateTime? newDeadline, Guid currentUserId);

    Task<Result<TaskModel>> AssignAsync(Guid taskId, Guid? assigneeId, Guid currentUserId);

    Task<Result<IReadOnlyList<AssigneeOptionModel>>> GetAssigneeCandidatesAsync(Guid taskId, Guid currentUserId);

}