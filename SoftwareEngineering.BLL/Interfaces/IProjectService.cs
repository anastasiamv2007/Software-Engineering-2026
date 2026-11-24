using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using static SoftwareEngineering.BLL.Models.ProjectModel;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IProjectService : IService
{
    Task<Result<IReadOnlyList<ProjectModel>>> GetUserProjectsAsync(Guid userId);

    Task<Result<ProjectModel>> GetByIdAsync(Guid id);

    Task<Result<ProjectModel>> CreateProjectAsync(CreateProjectModel model, Guid currentUserId);

    Task<Result> UpdateProjectAsync(UpdateProjectModel model, Guid currentUserId);

    Task<Result> DeleteProjectAsync(Guid id, Guid currentUserId);

    Task<Result<ProjectModel>> GetOrCreateByNameAsync(string name, Guid ownerId);
}
