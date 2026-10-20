namespace SoftwareEngineering.BLL.Models;

public class DatabaseConnectionModel {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "planora_db";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string BuildConnectionString() {
        return $"Host={Host.Trim()};Port={Port};Database={Database.Trim()};Username={Username.Trim()};Password={Password};Include Error Detail=true";
    }
}