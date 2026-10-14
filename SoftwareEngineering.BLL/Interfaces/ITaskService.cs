using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Interfaces;

public interface ITaskService : IService
{
    Task<TaskModel> CreateAsync(TaskModel model);

    // 2
    Task<TaskModel> GetByIdAsync(Guid id);

    //3
    Task<TaskModel> UpdateAsync(TaskModel model);

    // 4 delete task
    Task<TaskModel> DeleteTaskByOwnerAsync(TaskModel model, Guid currentUserId);
}