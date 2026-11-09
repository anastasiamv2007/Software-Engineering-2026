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

    /// <summary>
    /// Збережене підключення, до якого зараз під'єднано застосунок (до нього прив'язується «Запам'ятати мене»).
    /// </summary>
    public Guid? CurrentConnectionId { get; private set; }

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

    public void ConfigureServices(string connectionString, Guid connectionId)
    {
        // Якщо раніше було підключення до іншої бази — звільняємо старий контейнер
        DisposeServices();
        CurrentConnectionId = connectionId;

        var services = new ServiceCollection();

        // Реєстрація сервісів BLL і DAL
        services.AddBusinessLogic(connectionString);

        services.AddScoped<SoftwareEngineering.DAL.Repositories.ICommentRepository, SoftwareEngineering.DAL.Repositories.CommentRepository>();
        services.AddScoped<SoftwareEngineering.BLL.Services.CommentService>();

        services.AddSingleton<UserSession>();
        services.AddSingleton<IUserSession>(sp => sp.GetRequiredService<UserSession>());

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
    /// Збережений вхід для цієї бази забувається — до наступного входу з галочкою.
    /// </summary>
    public void SignOut()
    {
        var session = GetService<IUserSession>();
        Log.Information("Користувач {UserId} вийшов з облікового запису", session.CurrentUserId);
        ForgetCurrentSession();
        session.Clear();
        SwitchTo(GetService<LoginWindow>());
    }

    /// <summary>
    /// Відключення від бази: відкривається вікно вибору бази даних (без автоматичного підключення).
    /// Збережений вхід для цієї бази лишається — при наступному виборі цієї бази логін не знадобиться.
    /// </summary>
    public void SignOutAndDisconnect()
    {
        Log.Information(
            "Користувач {UserId} відключився від бази даних",
            ServiceProvider?.GetService<IUserSession>()?.CurrentUserId);
        SwitchTo(new DatabaseSelectionWindow(setupService, autoConnect: false));
        DisposeServices();
    }

    /// <summary>
    /// Пробує увійти за збереженою для поточної бази сесією. Недійсну сесію одразу забуває.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        if (CurrentConnectionId is not { } connectionId)
        {
            return false;
        }

        var remembered = setupService.GetRememberedSession(connectionId);
        if (remembered == null)
        {
            return false;
        }

        var user = await GetService<IAuthService>().RestoreSessionAsync(remembered);
        if (user == null)
        {
            setupService.ForgetSession(connectionId);
            return false;
        }

        GetService<IUserSession>().SetUser(user);
        return true;
    }

    /// <summary>
    /// Після входу: з галочкою — запам'ятовує акаунт для поточної бази, без неї — забуває попередній.
    /// </summary>
    public async Task UpdateRememberedSessionAsync(Guid userId, bool remember)
    {
        if (CurrentConnectionId is not { } connectionId)
        {
            return;
        }

        try
        {
            if (remember)
            {
                var session = await GetService<IAuthService>().CreateRememberedSessionAsync(userId);
                setupService.RememberSession(connectionId, session);
            }
            else
            {
                setupService.ForgetSession(connectionId);
            }
        }
        catch (Exception ex)
        {
            // Вхід уже виконано — невдале збереження сесії не має його зривати
            Log.Warning(ex, "Не вдалося оновити збережений вхід для підключення {ConnectionId}", connectionId);
        }
    }

    private void ForgetCurrentSession()
    {
        if (CurrentConnectionId is { } connectionId)
        {
            setupService.ForgetSession(connectionId);
        }
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
        // Під час запуску одразу підключаємось до останньої бази, якщо для неї збережено вхід
        new DatabaseSelectionWindow(setupService, autoConnect: true).Show();
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
        CurrentConnectionId = null;
    }
}
