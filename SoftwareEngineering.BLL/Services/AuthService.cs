using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class AuthService : BaseService, IAuthService
{
    private const int MinPasswordLength = 6;

    // Обмеження стовпців users (UserConfiguration): довший текст БД однаково не прийме
    private const int MaxFullNameLength = 150;
    private const int MaxEmailLength = 255;
    private const string UserNotFoundMessage = "Користувача не знайдено.";

    // Користувачу — одне загальне повідомлення, щоб не підказувати, чи існує такий email
    private const string InvalidCredentialsMessage = "Невірний email або пароль.";

    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IUserRepository userRepository;

    public AuthService(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    public async Task<Result<UserModel>> RegisterAsync(RegisterUserModel model, CancellationToken cancellationToken = default)
    {
        if (model is null || string.IsNullOrWhiteSpace(model.FullName))
        {
            return this.Fail<UserModel>(ErrorType.Validation, "Ім'я користувача не може бути порожнім.");
        }

        if (string.IsNullOrWhiteSpace(model.Email) || !EmailRegex.IsMatch(model.Email.Trim()))
        {
            return this.Fail<UserModel>(ErrorType.Validation, "Введено некоректний формат електронної пошти.");
        }

        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < MinPasswordLength)
        {
            return this.Fail<UserModel>(ErrorType.Validation, $"Пароль повинен містити щонайменше {MinPasswordLength} символів.");
        }

        var email = model.Email.Trim().ToLowerInvariant();
        if (await this.userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            return this.Fail<UserModel>(ErrorType.Duplicate, "Користувач із такою електронною поштою вже зареєстрований.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = model.FullName.Trim(),
            PasswordHash = PasswordHasher.Hash(model.Password),
            CreatedAt = DateTime.UtcNow,
        };

        await this.userRepository.AddAsync(user, cancellationToken);
        this.Logger.Information("Зареєстровано нового користувача {UserId} ({Email})", user.Id, user.Email);

        return Result.Ok(ToModel(user));
    }

    public async Task<Result<UserModel>> LoginAsync(LoginUserModel model, CancellationToken cancellationToken = default)
    {
        if (model is null || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            return this.Fail<UserModel>(ErrorType.Validation, "Вкажіть email та пароль.");
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var user = await this.userRepository.GetByEmailAsync(email, cancellationToken);

        // У лог — справжня причина, користувачу — загальне повідомлення
        if (user is null)
        {
            this.Logger.Warning("Невдала спроба входу {Email}: користувача не знайдено", email);
            return this.Fail<UserModel>(ErrorType.Validation, InvalidCredentialsMessage);
        }

        if (!PasswordHasher.Verify(model.Password, user.PasswordHash))
        {
            this.Logger.Warning("Невдала спроба входу {Email} (користувач {UserId}): невірний пароль", email, user.Id);
            return this.Fail<UserModel>(ErrorType.Validation, InvalidCredentialsMessage);
        }

        this.Logger.Information("Успішний вхід користувача {UserId} ({Email})", user.Id, email);
        return Result.Ok(ToModel(user));
    }

    public async Task<Result<UserModel>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? this.Fail<UserModel>(ErrorType.NotFound, UserNotFoundMessage)
            : Result.Ok(ToModel(user));
    }

    public async Task<Result<UserModel>> UpdateProfileAsync(Guid userId, string fullName, string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return this.Fail<UserModel>(ErrorType.Validation, "Ім'я користувача не може бути порожнім.");
        }

        if (fullName.Trim().Length > MaxFullNameLength)
        {
            return this.Fail<UserModel>(ErrorType.Validation, $"Ім'я не може бути довшим за {MaxFullNameLength} символів.");
        }

        if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email.Trim()) || email.Trim().Length > MaxEmailLength)
        {
            return this.Fail<UserModel>(ErrorType.Validation, "Введено некоректний формат електронної пошти.");
        }

        var user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return this.Fail<UserModel>(ErrorType.NotFound, UserNotFoundMessage);
        }

        // Той самий email у іншого користувача — відмова; власний (навіть в іншому регістрі) можна лишити
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var owner = await this.userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (owner is not null && owner.Id != userId)
        {
            return this.Fail<UserModel>(ErrorType.Duplicate, "Користувач із такою електронною поштою вже зареєстрований.");
        }

        user.FullName = fullName.Trim();
        user.Email = normalizedEmail;
        await this.userRepository.UpdateAsync(user, cancellationToken);

        this.Logger.Information("Оновлено профіль користувача {UserId}", user.Id);
        return Result.Ok(ToModel(user));
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oldPassword))
        {
            return this.Fail(ErrorType.Validation, "Введіть поточний пароль.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
        {
            return this.Fail(ErrorType.Validation, $"Новий пароль повинен містити щонайменше {MinPasswordLength} символів.");
        }

        if (oldPassword == newPassword)
        {
            return this.Fail(ErrorType.Validation, "Новий пароль повинен відрізнятися від поточного.");
        }

        var user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return this.Fail(ErrorType.NotFound, UserNotFoundMessage);
        }

        if (!PasswordHasher.Verify(oldPassword, user.PasswordHash))
        {
            return this.Fail(ErrorType.Validation, "Невірний поточний пароль.");
        }

        // Новий хеш змінює і PasswordStamp, тож збережений «Запам'ятати мене» на інших пристроях перестане діяти
        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await this.userRepository.UpdateAsync(user, cancellationToken);

        this.Logger.Information("Змінено пароль користувача {UserId}", user.Id);
        return Result.Ok();
    }

    public async Task<Result<RememberedSessionModel>> CreateRememberedSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return this.Fail<RememberedSessionModel>(ErrorType.NotFound, "Користувача не знайдено.");
        }

        return Result.Ok(new RememberedSessionModel
        {
            UserId = user.Id,
            PasswordStamp = CreatePasswordStamp(user.PasswordHash),
            CreatedAt = DateTime.UtcNow,
        });
    }

    public async Task<Result<UserModel>> RestoreSessionAsync(RememberedSessionModel session, CancellationToken cancellationToken = default)
    {
        if (session is null)
        {
            return this.Fail<UserModel>(ErrorType.Validation, "Збережений вхід не передано.");
        }

        var user = await this.userRepository.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            return this.Fail<UserModel>(ErrorType.NotFound, "Збережений вхід недійсний: користувача немає в цій базі.");
        }

        var expected = Encoding.UTF8.GetBytes(CreatePasswordStamp(user.PasswordHash));
        var actual = Encoding.UTF8.GetBytes(session.PasswordStamp ?? string.Empty);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return this.Fail<UserModel>(ErrorType.Validation, "Збережений вхід недійсний: пароль змінено.");
        }

        this.Logger.Information("Автоматичний вхід користувача {UserId} ({Email}) за збереженою сесією", user.Id, user.Email);
        return Result.Ok(ToModel(user));
    }

    // Відбиток хешу, а не сам хеш: на диску не лежить нічого, з чим можна підбирати пароль
    private static string CreatePasswordStamp(string passwordHash)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
    }

    private static UserModel ToModel(User user)
    {
        return new UserModel
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt,
        };
    }
}
