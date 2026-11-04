using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IProjectMemberService : IService
{
    /// <summary>
    /// Додає користувача (за email) до проєкту з роллю CoExecutor. Додавати може лише Owner проєкту.
    /// </summary>
    Task<Result> AddCoExecutorAsync(Guid projectId, string email, Guid currentUserId);
}
