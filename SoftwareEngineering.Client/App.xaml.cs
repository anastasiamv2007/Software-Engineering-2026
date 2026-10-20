using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SoftwareEngineering.BLL;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Services;

namespace SoftwareEngineering.Client;

public partial class App : Application {
    public IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        IDatabaseSetupService setupService = new DatabaseSetupService();
        string? connectionString = setupService.GetSavedConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString)) {
            // Хоста/бази ще немає — відкриваємо вікно налаштування бази
            var setupWindow = new DatabaseSetupWindow(setupService);
            setupWindow.Show();
            return;
        }

        // Рядок є — піднімаємо повноцінний DI контейнер
        ConfigureServices(connectionString);

        var loginWindow = ServiceProvider!.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    public void ConfigureServices(string connectionString) {
        var services = new ServiceCollection();

        // Реєстрація сервісів BLL і DAL
        services.AddBusinessLogic(connectionString);

        // Реєстрація вікон
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddTransient<CreateTaskView>();
        services.AddTransient<EditTaskWindow>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public T GetService<T>() where T : class {
        if (ServiceProvider == null) {
            throw new InvalidOperationException("Сервіси ще не ініціалізовані.");
        }

        return ServiceProvider.GetRequiredService<T>();
    }
}