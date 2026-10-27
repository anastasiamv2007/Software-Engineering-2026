using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IProjectService : IService
{
    Task<ProjectModel> GetOrCreateByNameAsync(string name, Guid ownerId);
}