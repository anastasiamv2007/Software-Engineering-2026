using Microsoft.Extensions.DependencyInjection;
using SoftwareEngineering.BLL;
using System.Windows;

namespace SoftwareEngineering.Client;

public partial class App : Application {
    private readonly ServiceProvider _serviceProvider;

    public App() {
        var services = new ServiceCollection();

        string connString = "Host=localhost;Port=5432;Database=planora_db;Username=postgres;Password=postgres;";

        services.AddBusinessLogic(connString);

        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}