using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.DAL;

namespace SoftwareEngineering.BLL.Services;

public class DatabaseSetupService : IDatabaseSetupService {
    // Найпростіше зберігати вибір користувача в налаштуваннях застосунку або локальному файлі
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Planora",
        "db_config.txt");

    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default) {
        try {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch {
            return false;
        }
    }

    public async Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken = default) {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var context = new AppDbContext(optionsBuilder.Options);

        // Автоматично створює БД (якщо дозволено правами) і застосовує всі непідтягнуті міграції
        await context.Database.MigrateAsync(cancellationToken);
    }

    public void SaveConnectionString(string connectionString) {
        string dir = Path.GetDirectoryName(ConfigFilePath)!;
        if (!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(ConfigFilePath, connectionString);
    }

    public string? GetSavedConnectionString() {
        return File.Exists(ConfigFilePath) ? File.ReadAllText(ConfigFilePath).Trim() : null;
    }
}