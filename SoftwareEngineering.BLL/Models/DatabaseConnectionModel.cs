using Npgsql;

namespace SoftwareEngineering.BLL.Models;

/// <summary>
/// Збережене підключення до бази даних PostgreSQL.
/// Пароль тримається тут у відкритому вигляді лише в пам'яті — на диск він потрапляє зашифрованим.
/// </summary>
public class DatabaseConnectionModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "planora_db";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Чи вдалося розшифрувати збережений пароль (наприклад, файл скопійовано з іншого ПК чи акаунту Windows).
    /// </summary>
    public bool IsPasswordAvailable { get; set; } = true;

    public string DisplayName => string.IsNullOrWhiteSpace(this.Name) ? $"{this.Database} @ {this.Host}" : this.Name;

    public string Summary => $"{this.Username}@{this.Host}:{this.Port}/{this.Database}";

    public string BuildConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = this.Host.Trim(),
            Port = this.Port,
            Database = this.Database.Trim(),
            Username = this.Username.Trim(),
            Password = this.Password,
            IncludeErrorDetail = true,
        };

        return builder.ConnectionString;
    }
}
