using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IAuthService
{
    Task<Result<UserModel>> RegisterAsync(RegisterUserModel model, CancellationToken cancellationToken = default);

    Task<Result<UserModel>> LoginAsync(LoginUserModel model, CancellationToken cancellationToken = default);

    Task<Result<UserModel>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Дані для «Запам'ятати мене» щойно залогіненого користувача.
    /// </summary>
    Task<Result<RememberedSessionModel>> CreateRememberedSessionAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Відновлює вхід без пароля; помилка, якщо користувача видалено або він змінив пароль.
    /// </summary>
    Task<Result<UserModel>> RestoreSessionAsync(RememberedSessionModel session, CancellationToken cancellationToken = default);
}
