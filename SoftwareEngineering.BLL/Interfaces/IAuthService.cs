using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IAuthService {
    Task<UserModel> RegisterAsync(RegisterUserModel model, CancellationToken cancellationToken = default);
    Task<UserModel> LoginAsync(LoginUserModel model, CancellationToken cancellationToken = default);
    Task<UserModel?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Дані для «Запам'ятати мене» щойно залогіненого користувача.
    /// </summary>
    Task<RememberedSessionModel> CreateRememberedSessionAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Відновлює вхід без пароля; null, якщо користувача видалено або він змінив пароль.
    /// </summary>
    Task<UserModel?> RestoreSessionAsync(RememberedSessionModel session, CancellationToken cancellationToken = default);
}