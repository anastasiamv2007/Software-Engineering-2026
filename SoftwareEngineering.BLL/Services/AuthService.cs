using System.Text.RegularExpressions;
using SoftwareEngineering.BLL.Exceptions;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class AuthService : IAuthService {
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository) {
        _userRepository = userRepository;
    }

    public async Task<UserModel> RegisterAsync(RegisterUserModel model, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(model.FullName))
            throw new ValidationException("Ім'я користувача не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(model.Email) || !Regex.IsMatch(model.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ValidationException("Введено некоректний формат електронної пошти.");

        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            throw new ValidationException("Пароль повинен містити щонайменше 6 символів.");

        string normalizedEmail = model.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
            throw new ValidationException("Користувач із такою електронною поштою вже зареєстрований.");

        var user = new User {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            PasswordHash = PasswordHasher.Hash(model.Password),
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new UserModel {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UserModel> LoginAsync(LoginUserModel model, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            throw new ValidationException("Вкажіть email та пароль.");

        var user = await _userRepository.GetByEmailAsync(model.Email, cancellationToken);
        if (user == null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
            throw new ValidationException("Невірний email або пароль.");

        return new UserModel {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        };
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