using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Interfaces;

public interface ITaskService : IService
{
    Task<TaskModel> CreateAsync(TaskModel model);

    // 2
    Task<TaskModel> GetByIdAsync(Guid id);
}