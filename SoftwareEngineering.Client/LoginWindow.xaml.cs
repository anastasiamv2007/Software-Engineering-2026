using System.Windows;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.Client;

public partial class LoginWindow : Window {
    private readonly IAuthService _authService;
    private readonly IUserSession _userSession;
    private bool _isRegisterMode = false;

    public LoginWindow(IAuthService authService, IUserSession userSession) {
        InitializeComponent();
        _authService = authService;
        _userSession = userSession;
    }

    private void SwitchModeButton_Click(object sender, RoutedEventArgs e) {
        _isRegisterMode = !_isRegisterMode;

        if (_isRegisterMode) {
            HeaderSubtitle.Text = "Створення нового акаунту";
            FullNameBlock.Visibility = Visibility.Visible;
            SubmitButton.Content = "Зареєструватися";
            SwitchModeButton.Content = "Вже маєте обліковий запис? Увійти";
        }
        else {
            HeaderSubtitle.Text = "Вхід у систему";
            FullNameBlock.Visibility = Visibility.Collapsed;
            SubmitButton.Content = "Увійти в кабінет";
            SwitchModeButton.Content = "Немає акаунту? Зареєструватися";
        }
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e) {
        // Кнопка ввімкнеться в кінці методу, навіть якщо БД недоступна і вилетить виняток
        using var busy = UiBusy.Disable(SubmitButton);

        var result = _isRegisterMode
            ? await _authService.RegisterAsync(new RegisterUserModel {
                FullName = FullNameBox.Text,
                Email = EmailBox.Text,
                Password = PasswordBox.Password
            })
            : await _authService.LoginAsync(new LoginUserModel {
                Email = EmailBox.Text,
                Password = PasswordBox.Password
            });

        if (result.ShowIfFailed(_isRegisterMode ? "Реєстрація не вдалася" : "Вхід не вдався")) {
            return;
        }

        var user = result.Value!;

        // Зберігаємо залогіненого користувача в спільну сесію
        _userSession.SetUser(user);

        var app = (App)Application.Current;
        await app.UpdateRememberedSessionAsync(user.Id, RememberMeCheckBox.IsChecked == true);

        // Відкриваємо головне робоче вікно
        var mainWindow = app.GetService<MainWindow>();
        mainWindow.Show();
        this.Close();
    }
}
