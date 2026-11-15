using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

public sealed record ProjectMemberOption(Guid UserId, string FullName, string Email);

public interface IProjectMemberService : IService
{
    /// <summary>
    /// Додає користувача (за email) до проєкту з роллю CoExecutor. Додавати може лише Owner проєкту.
    /// </summary>
    Task<Result> AddCoExecutorAsync(Guid projectId, string email, Guid currentUserId);
    Task<Result> TransferOwnershipAsync(Guid projectId, Guid newOwnerId, Guid currentUserId);
    Task<Result<IReadOnlyList<ProjectMemberOption>>> GetTransferCandidatesAsync(Guid projectId, Guid currentUserId);
}
