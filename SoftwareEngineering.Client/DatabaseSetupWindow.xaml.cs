using System.Windows;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.Client;

public partial class DatabaseSetupWindow : Window {
    private readonly IDatabaseSetupService _dbService;

    public DatabaseSetupWindow(IDatabaseSetupService dbService) {
        InitializeComponent();
        _dbService = dbService;
    }

    private DatabaseConnectionModel BuildModel() {
        return new DatabaseConnectionModel {
            Host = HostBox.Text,
            Port = int.TryParse(PortBox.Text, out var p) ? p : 5432,
            Database = DatabaseBox.Text,
            Username = UserBox.Text,
            Password = PasswordBox.Password
        };
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e) {
        var model = BuildModel();
        string connStr = model.BuildConnectionString();

        bool isAlive = await _dbService.TestConnectionAsync(connStr);
        if (isAlive) {
            MessageBox.Show("З'єднання з базою даних успішне!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else {
            MessageBox.Show("Не вдалося підключитися до бази. Перевірте хост, порт і логін/пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveAndContinue_Click(object sender, RoutedEventArgs e) {
        var model = BuildModel();
        string connStr = model.BuildConnectionString();

        try {
            // 1. Застосовуємо міграції (якщо перша людина — створить таблиці, якщо друга — просто перевірить)
            await _dbService.ApplyMigrationsAsync(connStr);
            _dbService.SaveConnectionString(connStr);

            // 2. Ініціалізуємо DI-контейнер з новим connection string
            var app = (App)Application.Current;
            app.ConfigureServices(connStr);

            // 3. Тепер сервіси налаштовані, і LoginWindow створиться без помилки
            var loginWindow = app.GetService<LoginWindow>();
            loginWindow.Show();
            this.Close();
        }
        catch (Exception ex) {
            MessageBox.Show($"Помилка ініціалізації бази даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


}