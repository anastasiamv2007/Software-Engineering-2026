using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Керує підключеннями до PostgreSQL: перевірка, міграції та список збережених баз
/// у %APPDATA%\Planora\connections.json (паролі зашифровані через DPAPI).
/// </summary>
public class DatabaseSetupService : IDatabaseSetupService
{
    private const string ConnectionsFileName = "connections.json";

    // Старий формат: один рядок підключення з паролем у відкритому вигляді
    private const string LegacyConfigFileName = "db_config.txt";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string storageDirectory;

    public DatabaseSetupService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Planora"))
    {
    }

    public DatabaseSetupService(string storageDirectory)
    {
        this.storageDirectory = storageDirectory;
    }

    // ForContext береться щоразу, щоб працювати з логером, налаштованим уже після створення сервісу
    private static ILogger Logger => Log.ForContext<DatabaseSetupService>();

    private string ConnectionsFilePath => Path.Combine(this.storageDirectory, ConnectionsFileName);

    private string LegacyConfigFilePath => Path.Combine(this.storageDirectory, LegacyConfigFileName);

    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var target = DescribeTarget(connectionString);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            Logger.Information(
                "Успішне підключення до БД {Database} на {Host}:{Port} користувачем {DbUser} (PostgreSQL {ServerVersion})",
                target.Database,
                target.Host,
                target.Port,
                target.Username,
                connection.ServerVersion);
            return true;
        }
        catch (PostgresException ex)
        {
            // Помилка від самого сервера: невірний пароль (28P01), немає бази (3D000) тощо
            Logger.Error(
                ex,
                "Помилка SQL під час підключення до БД {Database} на {Host}:{Port} користувачем {DbUser}: [{SqlState}] {SqlMessage}",
                target.Database,
                target.Host,
                target.Port,
                target.Username,
                ex.SqlState,
                ex.MessageText);
            return false;
        }
        catch (Exception ex)
        {
            // Сервер недоступний: не запущений, невірний хост/порт, мережа, тайм-аут
            Logger.Error(
                ex,
                "Не вдалося підключитися до БД {Database} на {Host}:{Port} користувачем {DbUser}",
                target.Database,
                target.Host,
                target.Port,
                target.Username);
            return false;
        }
    }

    public async Task ApplyMigrationsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

        await using var context = new AppDbContext(optionsBuilder.Options);
        var target = DescribeTarget(connectionString);

        try
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            // Автоматично створює БД (якщо дозволено правами) і застосовує всі непідтягнуті міграції
            await context.Database.MigrateAsync(cancellationToken);

            if (pending.Count > 0)
            {
                Logger.Information(
                    "До БД {Database} на {Host} застосовано міграції ({MigrationCount}): {Migrations}",
                    target.Database,
                    target.Host,
                    pending.Count,
                    pending);
            }
            else
            {
                Logger.Information("Структура БД {Database} на {Host} актуальна, міграції не потрібні", target.Database, target.Host);
            }
        }
        catch (PostgresException ex)
        {
            Logger.Error(
                ex,
                "Помилка SQL під час застосування міграцій до БД {Database} на {Host}: [{SqlState}] {SqlMessage}",
                target.Database,
                target.Host,
                ex.SqlState,
                ex.MessageText);
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Не вдалося застосувати міграції до БД {Database} на {Host}", target.Database, target.Host);
            throw;
        }
    }

    public IReadOnlyList<DatabaseConnectionModel> GetSavedConnections()
    {
        this.ImportLegacyConfig();

        return this.ReadStored()
            .Select(ToModel)
            .OrderByDescending(connection => connection.LastUsedAt ?? DateTime.MinValue)
            .ThenBy(connection => connection.DisplayName)
            .ToList()
            .AsReadOnly();
    }

    public void SaveConnection(DatabaseConnectionModel connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Id == Guid.Empty)
        {
            connection.Id = Guid.NewGuid();
        }

        var stored = this.ReadStored();
        stored.RemoveAll(item => item.Id == connection.Id);
        stored.Add(ToStored(connection));
        this.WriteStored(stored);

        Logger.Information(
            "Збережено підключення {ConnectionName} ({DbUser}@{Host}:{Port}/{Database})",
            connection.DisplayName,
            connection.Username,
            connection.Host,
            connection.Port,
            connection.Database);
    }

    public void DeleteConnection(Guid connectionId)
    {
        var stored = this.ReadStored();
        if (stored.RemoveAll(item => item.Id == connectionId) > 0)
        {
            this.WriteStored(stored);
            Logger.Information("Видалено збережене підключення {ConnectionId}", connectionId);
        }
    }

    public void MarkAsUsed(Guid connectionId)
    {
        var stored = this.ReadStored();
        var connection = stored.Find(item => item.Id == connectionId);
        if (connection == null)
        {
            return;
        }

        connection.LastUsedAt = DateTime.UtcNow;
        this.WriteStored(stored);
    }

    /// <summary>
    /// Дані для логів без пароля.
    /// </summary>
    private static (string? Host, int Port, string? Database, string? Username) DescribeTarget(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return (builder.Host, builder.Port, builder.Database, builder.Username);
        }
        catch (ArgumentException)
        {
            return (null, 0, null, null);
        }
    }

    private static DatabaseConnectionModel ToModel(StoredConnection stored)
    {
        string? password = SecretProtector.TryUnprotect(stored.ProtectedPassword);

        if (password == null && !string.IsNullOrEmpty(stored.ProtectedPassword))
        {
            Logger.Warning("Не вдалося розшифрувати пароль підключення {ConnectionName} (інший користувач Windows або ПК)", stored.Name);
        }

        return new DatabaseConnectionModel
        {
            Id = stored.Id,
            Name = stored.Name,
            Host = stored.Host,
            Port = stored.Port,
            Database = stored.Database,
            Username = stored.Username,
            Password = password ?? string.Empty,
            IsPasswordAvailable = password != null || string.IsNullOrEmpty(stored.ProtectedPassword),
            LastUsedAt = stored.LastUsedAt,
        };
    }

    private static StoredConnection ToStored(DatabaseConnectionModel model)
    {
        return new StoredConnection
        {
            Id = model.Id,
            Name = model.Name.Trim(),
            Host = model.Host.Trim(),
            Port = model.Port,
            Database = model.Database.Trim(),
            Username = model.Username.Trim(),
            ProtectedPassword = string.IsNullOrEmpty(model.Password) ? string.Empty : SecretProtector.Protect(model.Password),
            LastUsedAt = model.LastUsedAt,
        };
    }

    private List<StoredConnection> ReadStored()
    {
        if (!File.Exists(this.ConnectionsFilePath))
        {
            return new List<StoredConnection>();
        }

        try
        {
            string json = File.ReadAllText(this.ConnectionsFilePath);
            return JsonSerializer.Deserialize<List<StoredConnection>>(json, JsonOptions) ?? new List<StoredConnection>();
        }
        catch (JsonException ex)
        {
            Logger.Error(ex, "Файл збережених підключень пошкоджено: {Path}", this.ConnectionsFilePath);
            return new List<StoredConnection>();
        }
    }

    private void WriteStored(List<StoredConnection> stored)
    {
        Directory.CreateDirectory(this.storageDirectory);

        // Пишемо через тимчасовий файл, щоб збій під час запису не зіпсував список
        string tempPath = this.ConnectionsFilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(stored, JsonOptions));
        File.Move(tempPath, this.ConnectionsFilePath, overwrite: true);
    }

    /// <summary>
    /// Переносить старий db_config.txt у новий список (вже із зашифрованим паролем) і видаляє його.
    /// </summary>
    private void ImportLegacyConfig()
    {
        if (!File.Exists(this.LegacyConfigFilePath))
        {
            return;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(File.ReadAllText(this.LegacyConfigFilePath).Trim());

            this.SaveConnection(new DatabaseConnectionModel
            {
                Name = builder.Database ?? "planora_db",
                Host = builder.Host ?? "localhost",
                Port = builder.Port,
                Database = builder.Database ?? "planora_db",
                Username = builder.Username ?? string.Empty,
                Password = builder.Password ?? string.Empty,
            });

            File.Delete(this.LegacyConfigFilePath);
            Logger.Information("Старе підключення з {Path} перенесено у зашифрований список", this.LegacyConfigFilePath);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or IOException)
        {
            Logger.Warning(ex, "Не вдалося перенести старе підключення з {Path}", this.LegacyConfigFilePath);
        }
    }

    private sealed class StoredConnection
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Host { get; set; } = string.Empty;

        public int Port { get; set; }

        public string Database { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string ProtectedPassword { get; set; } = string.Empty;

        public DateTime? LastUsedAt { get; set; }
    }
}
