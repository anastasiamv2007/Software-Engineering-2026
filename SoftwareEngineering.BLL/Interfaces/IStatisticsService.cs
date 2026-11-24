using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IStatisticsService : IService
{
    Task<Result<TaskStatisticsModel>> GetStatisticsAsync(Guid userId, Guid? projectId = null);
}