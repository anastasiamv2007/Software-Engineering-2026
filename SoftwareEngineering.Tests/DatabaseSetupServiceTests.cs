using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;

namespace SoftwareEngineering.Tests;

public sealed class DatabaseSetupServiceTests : IDisposable
{
    private const string Password = "S3cret;Pa$$";

    private readonly string storageDirectory = Path.Combine(Path.GetTempPath(), "PlanoraTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(this.storageDirectory))
        {
            Directory.Delete(this.storageDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveConnection_ThenGetSavedConnections_ReturngsDecryptedPassword()
    {
        // Arrange
        var service = new DatabaseSetupService(this.storageDirectory);

        // Act
        service.SaveConnection(CreateConnection("Локальна"));
        var saved = Assert.Single(service.GetSavedConnections());

        // Assert
        Assert.Equal("Локальна", saved.Name);
        Assert.Equal(Password, saved.Password);
        Assert.True(saved.IsPasswordAvailable);
    }

    [Fact]
    public void SaveConnection_PasswordIsNotStoredInPlainText()
    {
        // Arrange
        var service = new DatabaseSetupService(this.storageDirectory);

        // Act
        service.SaveConnection(CreateConnection("Локальна"));

        // Assert
        string fileContent = File.ReadAllText(Path.Combine(this.storageDirectory, "connections.json"));
        Assert.DoesNotContain(Password, fileContent);
    }

    [Fact]
    public void SaveConnection_SameId_UpdatesInsteadOfDuplicating()
    {
        // Arrange
        var service = new DatabaseSetupService(this.storageDirectory);
        var connection = CreateConnection("Стара назва");
        service.SaveConnection(connection);

        // Act
        connection.Name = "Нова назва";
        service.SaveConnection(connection);

        // Assert
        var saved = Assert.Single(service.GetSavedConnections());
        Assert.Equal("Нова назва", saved.Name);
    }

    [Fact]
    public void DeleteConnection_RemovesOnlyThatConnection()
    {
        // Arrange
        var service = new DatabaseSetupService(this.storageDirectory);
        var first = CreateConnection("Перша");
        var second = CreateConnection("Друга");
        service.SaveConnection(first);
        service.SaveConnection(second);

        // Act
        service.DeleteConnection(first.Id);

        // Assert
        var saved = Assert.Single(service.GetSavedConnections());
        Assert.Equal(second.Id, saved.Id);
    }

    [Fact]
    public void MarkAsUsed_MovesConnectionToTop()
    {
        // Arrange
        var service = new DatabaseSetupService(this.storageDirectory);
        var first = CreateConnection("Перша");
        var second = CreateConnection("Друга");
        service.SaveConnection(first);
        service.SaveConnection(second);

        // Act
        service.MarkAsUsed(second.Id);

        // Assert
        var saved = service.GetSavedConnections();
        Assert.Equal(second.Id, saved[0].Id);
        Assert.NotNull(saved[0].LastUsedAt);
    }

    [Fact]
    public void GetSavedConnections_LegacyPlainTextConfig_IsImportedEncryptedAndDeleted()
    {
        // Arrange
        Directory.CreateDirectory(this.storageDirectory);
        string legacyPath = Path.Combine(this.storageDirectory, "db_config.txt");
        File.WriteAllText(legacyPath, "Host=localhost;Port=5432;Database=planora_db;Username=postgres;Password=2007");
        var service = new DatabaseSetupService(this.storageDirectory);

        // Act
        var saved = Assert.Single(service.GetSavedConnections());

        // Assert
        Assert.Equal("planora_db", saved.Database);
        Assert.Equal("2007", saved.Password);
        Assert.False(File.Exists(legacyPath));
        Assert.DoesNotContain("2007", File.ReadAllText(Path.Combine(this.storageDirectory, "connections.json")));
    }

    [Fact]
    public void BuildConnectionString_PasswordWithSemicolon_IsEscaped()
    {
        // Arrange
        var connection = CreateConnection("Локальна");

        // Act
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connection.BuildConnectionString());

        // Assert
        Assert.Equal(Password, builder.Password);
    }

    private static DatabaseConnectionModel CreateConnection(string name)
    {
        return new DatabaseConnectionModel
        {
            Name = name,
            Host = "localhost",
            Port = 5432,
            Database = "planora_db",
            Username = "postgres",
            Password = Password,
        };
    }
}
