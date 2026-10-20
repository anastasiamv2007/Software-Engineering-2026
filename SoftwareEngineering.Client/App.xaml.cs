using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SoftwareEngineering.BLL;
using System.Windows;


namespace SoftwareEngineering.Client;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<App>()
            .AddEnvironmentVariables()
            .Build();

        string connString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Не знайдено рядок підключення 'ConnectionStrings:Default'. Додайте його через Manage User Secrets.");

        services.AddBusinessLogic(connString);

        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}