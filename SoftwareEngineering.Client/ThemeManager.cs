using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Serilog;

namespace SoftwareEngineering.Client;

/// <summary>
/// Назви збігаються з файлами палітр у Themes/ (крім System, яка обирає Light або Dark за налаштуванням Windows).
/// </summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
    Black,
    DarkBlue,
}

/// <summary>
/// Теми оформлення. Палітра — перший словник у App.xaml; розмітка бере кольори через DynamicResource,
/// тому підміна словника одразу перемальовує всі відкриті вікна.
/// Вибір зберігається в реєстрі поточного користувача Windows (HKCU\Software\Planora), а не в БД:
/// це налаштування комп'ютера, а не облікового запису.
/// </summary>
internal static class ThemeManager
{
    private const string SettingsKeyPath = @"Software\Planora";
    private const string ThemeValueName = "Theme";
    private const string LastDarkValueName = "LastDarkTheme";
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // DWMWA_USE_IMMERSIVE_DARK_MODE: темний системний заголовок вікна (Windows 10 20H1+ і Windows 11)
    private const int DwmUseImmersiveDarkMode = 20;

    public static event EventHandler? ThemeChanged;

    public static AppTheme Preference { get; private set; } = AppTheme.System;

    /// <summary>
    /// Темна тема, яку обирали останньою, — на неї перемикає швидка кнопка в меню зі світлої.
    /// </summary>
    public static AppTheme LastDarkTheme { get; private set; } = AppTheme.Dark;

    public static bool IsDark { get; private set; }

    public static void Initialize()
    {
        Preference = ReadSetting(ThemeValueName, AppTheme.System);
        LastDarkTheme = ReadSetting(LastDarkValueName, AppTheme.Dark);
        Apply();

        // Кожне нове вікно одразу отримує заголовок у тон теми
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) => UpdateTitleBar((Window)sender)));

        // Якщо обрано «як у Windows», слідкуємо за перемиканням теми в системі
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == UserPreferenceCategory.General && Preference == AppTheme.System)
            {
                Application.Current.Dispatcher.BeginInvoke(Apply);
            }
        };
    }

    public static void SetPreference(AppTheme theme)
    {
        Preference = theme;
        using (var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
        {
            key.SetValue(ThemeValueName, theme.ToString());
            if (theme is not (AppTheme.System or AppTheme.Light))
            {
                LastDarkTheme = theme;
                key.SetValue(LastDarkValueName, theme.ToString());
            }
        }

        Log.Information("Обрано тему оформлення: {Theme}", theme);
        Apply();
    }

    private static void Apply()
    {
        var palette = Preference == AppTheme.System
            ? (WindowsUsesDarkTheme() ? AppTheme.Dark : AppTheme.Light)
            : Preference;
        IsDark = palette != AppTheme.Light;

        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{palette}.xaml", UriKind.Absolute),
        };

        foreach (Window window in Application.Current.Windows)
        {
            UpdateTitleBar(window);
        }

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static AppTheme ReadSetting(string name, AppTheme fallback)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
        return Enum.TryParse(key?.GetValue(name) as string, out AppTheme saved) && Enum.IsDefined(saved) ? saved : fallback;
    }

    // AppsUseLightTheme = 0 означає, що в налаштуваннях Windows для програм обрано темний режим
    private static bool WindowsUsesDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private static void UpdateTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        // На старих Windows виклик просто повертає код помилки і нічого не змінює
        var useDark = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref useDark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
