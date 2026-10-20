using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Interfaces;

public interface ITaskService : IService
{
    Task<TaskModel> CreateAsync(TaskModel model);


    // 7 update progress
    Task<TaskModel> UpdateProgressAsync(Guid taskId, TaskItemStatus newStatus, Guid currentUserId);

    // 2
    Task<TaskModel> GetByIdAsync(Guid id);

    //3
    Task<TaskModel> UpdateAsync(TaskModel model);

    // 4 delete task
    Task<TaskModel> DeleteTaskByOwnerAsync(TaskModel model, Guid currentUserId);

    // 6
    Task<TaskModel> UpdateDeadlineAsync(Guid taskId, DateTime? newDeadline, Guid currentUserId);

}