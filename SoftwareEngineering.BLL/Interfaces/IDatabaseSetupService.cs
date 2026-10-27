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
}
