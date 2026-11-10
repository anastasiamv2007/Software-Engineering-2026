using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IDatabaseSetupService
{
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Повертає збережені підключення: спочатку ті, що використовувались останніми.
    /// </summary>
    IReadOnlyList<DatabaseConnectionModel> GetSavedConnections();

    /// <summary>
    /// Додає нове підключення або оновлює існуюче з тим самим Id. Пароль шифрується перед записом на диск.
    /// </summary>
    void SaveConnection(DatabaseConnectionModel connection);

    void DeleteConnection(Guid connectionId);

    void MarkAsUsed(Guid connectionId);

    /// <summary>
    /// Збережений вхід для цієї бази (null, якщо його немає або його не вдалося розшифрувати).
    /// </summary>
    RememberedSessionModel? GetRememberedSession(Guid connectionId);

    /// <summary>
    /// Запам'ятовує вхід для бази; попередній збережений акаунт цієї бази замінюється.
    /// </summary>
    void RememberSession(Guid connectionId, RememberedSessionModel session);

    void ForgetSession(Guid connectionId);
}
