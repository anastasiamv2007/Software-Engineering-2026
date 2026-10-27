using System.Text.RegularExpressions;
using Serilog;
using SoftwareEngineering.BLL.Exceptions;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class AuthService : IAuthService {
    private readonly IUserRepository _userRepository;

    private static ILogger Logger => Log.ForContext<AuthService>();

    public AuthService(IUserRepository userRepository) {
        _userRepository = userRepository;
    }

    public async Task<UserModel> RegisterAsync(RegisterUserModel model, CancellationToken cancellationToken = default) {
        string? email = model.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(model.FullName))
            throw RegistrationFailed(email, "Ім'я користувача не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(model.Email) || !Regex.IsMatch(model.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw RegistrationFailed(email, "Введено некоректний формат електронної пошти.");

        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            throw RegistrationFailed(email, "Пароль повинен містити щонайменше 6 символів.");

        string normalizedEmail = model.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
            throw RegistrationFailed(normalizedEmail, "Користувач із такою електронною поштою вже зареєстрований.");

        var user = new User {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            PasswordHash = PasswordHasher.Hash(model.Password),
            CreatedAt = DateTime.UtcNow
        };

        try {
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "Помилка БД під час реєстрації користувача {Email}", normalizedEmail);
            throw;
        }

        Logger.Information("Зареєстровано нового користувача {UserId} ({Email})", user.Id, user.Email);

        return new UserModel {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UserModel> LoginAsync(LoginUserModel model, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password)) {
            Logger.Warning("Невдала спроба входу: не вказано email або пароль");
            throw new ValidationException("Вкажіть email та пароль.");
        }

        string email = model.Email.Trim().ToLowerInvariant();

        User? user;
        try {
            user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "Помилка БД під час входу користувача {Email}", email);
            throw;
        }

        // Користувачу показуємо одне загальне повідомлення, а в лог пишемо справжню причину
        if (user == null) {
            Logger.Warning("Невдала спроба входу {Email}: користувача не знайдено", email);
            throw new ValidationException("Невірний email або пароль.");
        }

        if (!PasswordHasher.Verify(model.Password, user.PasswordHash)) {
            Logger.Warning("Невдала спроба входу {Email} (користувач {UserId}): невірний пароль", email, user.Id);
            throw new ValidationException("Невірний email або пароль.");
        }

        Logger.Information("Успішний вхід користувача {UserId} ({Email})", user.Id, email);

        return new UserModel {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        };
    }

    private static ValidationException RegistrationFailed(string? email, string reason) {
        Logger.Warning("Невдала реєстрація {Email}: {Reason}", email, reason);
        return new ValidationException(reason);
    }

    public async Task<UserModel?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default) {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return null;

        return new UserModel {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        };
    }
}