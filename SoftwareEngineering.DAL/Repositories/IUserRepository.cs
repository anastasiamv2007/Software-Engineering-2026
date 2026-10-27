using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface IUserRepository {
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    /// <summary>
    /// Додає користувача і одразу зберігає зміни в БД.
    /// </summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}