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

    // Користувачу — одне загальне повідомлення, щоб не підказувати, чи існує такий email
    private const string InvalidCredentialsMessage = "Невірний email або пароль.";

    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IUserRepository userRepository;

    public AuthService(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    public Task<Result<UserModel>> RegisterAsync(RegisterUserModel model, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync("Реєстрація користувача", async () =>
        {
            if (model is null || string.IsNullOrWhiteSpace(model.FullName))
            {
                return Result.Fail<UserModel>(ErrorType.Validation, "Ім'я користувача не може бути порожнім.");
            }

            if (string.IsNullOrWhiteSpace(model.Email) || !EmailRegex.IsMatch(model.Email.Trim()))
            {
                return Result.Fail<UserModel>(ErrorType.Validation, "Введено некоректний формат електронної пошти.");
            }

            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < MinPasswordLength)
            {
                return Result.Fail<UserModel>(ErrorType.Validation, $"Пароль повинен містити щонайменше {MinPasswordLength} символів.");
            }

            var email = model.Email.Trim().ToLowerInvariant();
            if (await this.userRepository.ExistsByEmailAsync(email, cancellationToken))
            {
                return Result.Fail<UserModel>(ErrorType.Duplicate, "Користувач із такою електронною поштою вже зареєстрований.");
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
        });
    }

    public Task<Result<UserModel>> LoginAsync(LoginUserModel model, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync("Вхід користувача", async () =>
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                return Result.Fail<UserModel>(ErrorType.Validation, "Вкажіть email та пароль.");
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var user = await this.userRepository.GetByEmailAsync(email, cancellationToken);

            // У лог — справжня причина, користувачу — загальне повідомлення
            if (user is null)
            {
                this.Logger.Warning("Невдала спроба входу {Email}: користувача не знайдено", email);
                return Result.Fail<UserModel>(ErrorType.Validation, InvalidCredentialsMessage);
            }

            if (!PasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                this.Logger.Warning("Невдала спроба входу {Email} (користувач {UserId}): невірний пароль", email, user.Id);
                return Result.Fail<UserModel>(ErrorType.Validation, InvalidCredentialsMessage);
            }

            this.Logger.Information("Успішний вхід користувача {UserId} ({Email})", user.Id, email);
            return Result.Ok(ToModel(user));
        });
    }

    public Task<Result<UserModel>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync("Отримання користувача", async () =>
        {
            var user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
            return user is null
                ? Result.Fail<UserModel>(ErrorType.NotFound, "Користувача не знайдено.")
                : Result.Ok(ToModel(user));
        });
    }

    public Task<Result<RememberedSessionModel>> CreateRememberedSessionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync("Збереження входу", async () =>
        {
            var user = await this.userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
            {
                return Result.Fail<RememberedSessionModel>(ErrorType.NotFound, "Користувача не знайдено.");
            }

            return Result.Ok(new RememberedSessionModel
            {
                UserId = user.Id,
                PasswordStamp = CreatePasswordStamp(user.PasswordHash),
                CreatedAt = DateTime.UtcNow,
            });
        });
    }

    public Task<Result<UserModel>> RestoreSessionAsync(RememberedSessionModel session, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync("Автоматичний вхід за збереженою сесією", async () =>
        {
            if (session is null)
            {
                return Result.Fail<UserModel>(ErrorType.Validation, "Збережений вхід не передано.");
            }

            var user = await this.userRepository.GetByIdAsync(session.UserId, cancellationToken);
            if (user is null)
            {
                return Result.Fail<UserModel>(ErrorType.NotFound, "Збережений вхід недійсний: користувача немає в цій базі.");
            }

            var expected = Encoding.UTF8.GetBytes(CreatePasswordStamp(user.PasswordHash));
            var actual = Encoding.UTF8.GetBytes(session.PasswordStamp ?? string.Empty);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                return Result.Fail<UserModel>(ErrorType.Validation, "Збережений вхід недійсний: пароль змінено.");
            }

            this.Logger.Information("Автоматичний вхід користувача {UserId} ({Email}) за збереженою сесією", user.Id, user.Email);
            return Result.Ok(ToModel(user));
        });
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
