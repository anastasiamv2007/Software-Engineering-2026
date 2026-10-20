namespace SoftwareEngineering.BLL.Interfaces;

public interface IDatabaseSetupService
{
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
    Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);
    void SaveConnectionString(string connectionString);
    string? GetSavedConnectionString();
}