using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IAuthService {
    Task<UserModel> RegisterAsync(RegisterUserModel model, CancellationToken cancellationToken = default);
    Task<UserModel> LoginAsync(LoginUserModel model, CancellationToken cancellationToken = default);
    Task<UserModel?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}