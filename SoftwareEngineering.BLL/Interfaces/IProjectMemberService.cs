using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IProjectMemberService : IService
{
    /// <summary>
    /// Додає користувача (за email) до проєкту з роллю CoExecutor. Додавати може лише Owner проєкту.
    /// </summary>
    Task<Result> AddCoExecutorAsync(Guid projectId, string email, Guid currentUserId);
    Task<Result<IReadOnlyList<ProjectMemberModel>>> GetMembersAsync(Guid projectId, Guid currentUserId);
    Task<Result> RemoveMemberAsync(Guid projectId, Guid memberId, Guid currentUserId);
}
