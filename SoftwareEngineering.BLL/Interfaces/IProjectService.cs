using SoftwareEngineering.BLL.Models;
using static SoftwareEngineering.BLL.Models.ProjectModel;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IProjectService : IService
{
    Task<IEnumerable<ProjectModel>> GetUserProjectsAsync(Guid userId);

    Task<ProjectModel?> GetByIdAsync(Guid id);

    Task<ProjectModel> CreateProjectAsync(CreateProjectModel model, Guid currentUserId);

    Task UpdateProjectAsync(UpdateProjectModel model);

    Task DeleteProjectAsync(Guid id);

    Task<ProjectModel> GetOrCreateByNameAsync(string name, Guid ownerId);
}