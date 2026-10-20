using System.Windows;
using SoftwareEngineering.BLL.Exceptions;
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
        try {
            UserModel user;

            if (_isRegisterMode) {
                user = await _authService.RegisterAsync(new RegisterUserModel {
                    FullName = FullNameBox.Text,
                    Email = EmailBox.Text,
                    Password = PasswordBox.Password
                });
            }
            else {
                user = await _authService.LoginAsync(new LoginUserModel {
                    Email = EmailBox.Text,
                    Password = PasswordBox.Password
                });
            }

            // Зберігаємо залогіненого користувача в спільну сесію
            _userSession.SetUser(user);

            // Відкриваємо головне робоче вікно
            var mainWindow = ((App)Application.Current).GetService<MainWindow>();
            mainWindow.Show();
            this.Close();
        }
        catch (ValidationException ex) {
            MessageBox.Show(ex.Message, "Помилка валідації", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) {
            MessageBox.Show($"Виникла неочікувана помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}