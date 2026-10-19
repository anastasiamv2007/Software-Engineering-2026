using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Interfaces;

public interface ITaskService : IService
{
    Task<TaskModel> CreateAsync(TaskModel model);

    // 7 update progress
    Task<TaskModel> UpdateProgressAsync(Guid taskId, TaskItemStatus newStatus, Guid currentUserId);
}