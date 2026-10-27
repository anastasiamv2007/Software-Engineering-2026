using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SoftwareEngineering.BLL;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Services;
using System.Windows;


namespace SoftwareEngineering.Client;

public partial class App : Application
{
    // Локальний Seq (http://localhost:5341 — веб-інтерфейс для перегляду логів)
    private const string SeqServerUrl = "http://localhost:5341";

    private readonly IDatabaseSetupService setupService = new DatabaseSetupService();

    public IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureLogging();

        // Завжди починаємо з вибору бази: звідти вже відкривається вікно логіну
        ShowDatabaseSelection();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeServices();
        Log.Information("Застосунок Planora закрито");

        // Якщо Seq не запущений, відправка висить кілька секунд — не тримаємо через це закриття вікна
        Task.Run(Log.CloseAndFlush).Wait(TimeSpan.FromSeconds(1));
        base.OnExit(e);
    }

    public void ConfigureServices(string connectionString)
    {
        // Якщо раніше було підключення до іншої бази — звільняємо старий контейнер
        DisposeServices();

        var services = new ServiceCollection();

        // Реєстрація сервісів BLL і DAL
        services.AddBusinessLogic(connectionString);

        // Реєстрація вікон
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();


        ServiceProvider = services.BuildServiceProvider();
    }

    public T GetService<T>() where T : class
    {
        if (ServiceProvider == null)
        {
            throw new InvalidOperationException("Сервіси ще не ініціалізовані.");
        }

        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Вихід з облікового запису: база лишається підключеною, відкривається вікно логіну.
    /// </summary>
    public void SignOut()
    {
        var session = GetService<IUserSession>();
        Log.Information("Користувач {UserId} вийшов з облікового запису", session.CurrentUserId);
        session.Clear();
        SwitchTo(GetService<LoginWindow>());
    }

    /// <summary>
    /// Вихід з облікового запису та відключення від бази: відкривається вікно вибору бази даних.
    /// </summary>
    public void SignOutAndDisconnect()
    {
        Log.Information(
            "Користувач {UserId} вийшов з облікового запису та відключився від бази даних",
            ServiceProvider?.GetService<IUserSession>()?.CurrentUserId);
        SwitchTo(new DatabaseSelectionWindow(setupService));
        DisposeServices();
    }

    /// <summary>
    /// Debug-збірка пише логи в Seq; Release поки без нього. Якщо Seq не запущений —
    /// події просто відкидаються, на роботу застосунку це не впливає.
    /// </summary>
    private static void ConfigureLogging()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("Application", "Planora.Client")
            .Enrich.WithProperty("MachineName", Environment.MachineName);

#if DEBUG
        configuration = configuration.WriteTo.Seq(SeqServerUrl);
#endif

        Log.Logger = configuration.CreateLogger();
        Log.Information("Застосунок Planora запущено");
    }

    private void ShowDatabaseSelection()
    {
        new DatabaseSelectionWindow(setupService).Show();
    }

    /// <summary>
    /// Показує нове вікно і закриває всі інші (спочатку Show, щоб застосунок не завершився).
    /// </summary>
    private void SwitchTo(Window next)
    {
        var previous = Windows.Cast<Window>().Where(window => window != next).ToList();

        next.Show();
        MainWindow = next;

        foreach (var window in previous)
        {
            window.Close();
        }
    }

    private void DisposeServices()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        // Сесія — singleton у контейнері, тож разом з ним зникає і поточний користувач
        ServiceProvider = null;
    }
}
