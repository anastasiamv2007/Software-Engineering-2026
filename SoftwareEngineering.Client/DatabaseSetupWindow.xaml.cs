using System.Windows;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.Client;

/// <summary>
/// Діалог створення або редагування збереженого підключення до БД.
/// </summary>
public partial class DatabaseSetupWindow : Window {
    private readonly IDatabaseSetupService _dbService;
    private readonly DatabaseConnectionModel? _existing;

    public DatabaseSetupWindow(IDatabaseSetupService dbService, DatabaseConnectionModel? existing = null) {
        InitializeComponent();
        _dbService = dbService;
        _existing = existing;

        if (existing != null) {
            HeaderSubtitle.Text = "Редагування підключення";
            NameBox.Text = existing.Name;
            HostBox.Text = existing.Host;
            PortBox.Text = existing.Port.ToString();
            DatabaseBox.Text = existing.Database;
            UserBox.Text = existing.Username;
            PasswordBox.Password = existing.Password;
        }
    }

    /// <summary>
    /// Збережене підключення (заповнюється після успішного «Зберегти»).
    /// </summary>
    public DatabaseConnectionModel? SavedConnection { get; private set; }

    private DatabaseConnectionModel BuildModel() {
        return new DatabaseConnectionModel {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Name = NameBox.Text,
            Host = HostBox.Text,
            Port = int.TryParse(PortBox.Text, out var p) ? p : 5432,
            Database = DatabaseBox.Text,
            Username = UserBox.Text,
            Password = PasswordBox.Password,
            LastUsedAt = _existing?.LastUsedAt
        };
    }

    private string? Validate(DatabaseConnectionModel model) {
        if (string.IsNullOrWhiteSpace(model.Host)) return "Вкажіть хост.";
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535) return "Порт має бути числом від 1 до 65535.";
        if (string.IsNullOrWhiteSpace(model.Database)) return "Вкажіть назву бази даних.";
        if (string.IsNullOrWhiteSpace(model.Username)) return "Вкажіть користувача.";
        return null;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e) {
        var model = BuildModel();
        string? error = Validate(model);
        if (error != null) {
            MessageBox.Show(error, "Перевірте дані", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetBusy(true);
        bool isAlive = await _dbService.TestConnectionAsync(model.BuildConnectionString());
        SetBusy(false);

        if (isAlive) {
            MessageBox.Show("З'єднання з базою даних успішне!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else {
            MessageBox.Show("Не вдалося підключитися до бази. Перевірте хост, порт і логін/пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) {
        var model = BuildModel();
        string? error = Validate(model);
        if (error != null) {
            MessageBox.Show(error, "Перевірте дані", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(model.Name)) {
            model.Name = $"{model.Database.Trim()} @ {model.Host.Trim()}";
        }

        try {
            _dbService.SaveConnection(model);
            SavedConnection = model;
            DialogResult = true;
        }
        catch (Exception ex) {
            MessageBox.Show($"Не вдалося зберегти підключення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetBusy(bool isBusy) {
        TestButton.IsEnabled = !isBusy;
        SaveButton.IsEnabled = !isBusy;
        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : null;
    }
}
