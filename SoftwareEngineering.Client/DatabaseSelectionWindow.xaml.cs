using Serilog;
using System.Windows;
using System.Windows.Input;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.Client;

/// <summary>
/// Стартове вікно: список збережених баз даних, з яких користувач обирає, куди підключитися.
/// </summary>
public partial class DatabaseSelectionWindow : Window
{
    private readonly IDatabaseSetupService dbService;
    private bool isBusy;

    public DatabaseSelectionWindow(IDatabaseSetupService dbService)
    {
        InitializeComponent();
        this.dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));

        Loaded += (_, _) =>
        {
            ReloadConnections();

            // Перший запуск — одразу пропонуємо додати базу
            if (ConnectionsList.Items.Count == 0)
            {
                OpenEditor(null);
            }
        };
    }

    private DatabaseConnectionModel? SelectedConnection => ConnectionsList.SelectedItem as DatabaseConnectionModel;

    private void ReloadConnections(Guid? selectId = null)
    {
        var connections = dbService.GetSavedConnections();
        ConnectionsList.ItemsSource = connections;
        EmptyState.Visibility = connections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ConnectionsList.SelectedItem = connections.FirstOrDefault(c => c.Id == selectId) ?? connections.FirstOrDefault();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = SelectedConnection != null;
        AddButton.IsEnabled = !isBusy;
        EditButton.IsEnabled = !isBusy && hasSelection;
        DeleteButton.IsEnabled = !isBusy && hasSelection;
        ConnectButton.IsEnabled = !isBusy && hasSelection;
        ConnectionsList.IsEnabled = !isBusy;
        Cursor = isBusy ? Cursors.Wait : null;
    }

    private void SetBusy(bool busy, string status = "")
    {
        isBusy = busy;
        StatusText.Text = status;
        UpdateButtons();
    }

    private bool OpenEditor(DatabaseConnectionModel? existing)
    {
        var editor = new DatabaseSetupWindow(dbService, existing) { Owner = this };
        if (editor.ShowDialog() == true && editor.SavedConnection != null)
        {
            ReloadConnections(editor.SavedConnection.Id);
            return true;
        }

        return false;
    }

    private void ConnectionsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        StatusText.Text = string.Empty;
        UpdateButtons();
    }

    private async void ConnectionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedConnection != null && !isBusy)
        {
            await ConnectAsync(SelectedConnection);
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        OpenEditor(null);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection != null)
        {
            OpenEditor(SelectedConnection);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection is not { } connection)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Видалити підключення «{connection.DisplayName}» зі списку?\nСама база даних залишиться без змін.",
            "Видалення підключення",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            dbService.DeleteConnection(connection.Id);
            ReloadConnections();
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedConnection != null)
        {
            await ConnectAsync(SelectedConnection);
        }
    }

    private async Task ConnectAsync(DatabaseConnectionModel connection)
    {
        if (!connection.IsPasswordAvailable)
        {
            MessageBox.Show(
                "Пароль цього підключення не вдалося розшифрувати (його збережено іншим користувачем Windows або на іншому ПК). Введіть пароль ще раз.",
                "Потрібен пароль",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            if (!OpenEditor(connection))
            {
                return;
            }

            connection = SelectedConnection ?? connection;
        }

        string connectionString = connection.BuildConnectionString();
        Log.Information("Підключення до збереженої БД {ConnectionName} ({Summary})", connection.DisplayName, connection.Summary);

        try
        {
            SetBusy(true, $"Підключення до «{connection.DisplayName}»...");

            if (!await dbService.TestConnectionAsync(connectionString))
            {
                SetBusy(false, "Не вдалося підключитися. Перевірте хост, порт і логін/пароль (кнопка «Редагувати»).");
                return;
            }

            SetBusy(true, "Перевірка структури бази (міграції)...");
            await dbService.ApplyMigrationsAsync(connectionString);
            dbService.MarkAsUsed(connection.Id);

            var app = (App)Application.Current;
            app.ConfigureServices(connectionString);

            var loginWindow = app.GetService<LoginWindow>();
            loginWindow.Show();
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не вдалося ініціалізувати підключення {ConnectionName}", connection.DisplayName);
            SetBusy(false, string.Empty);
            MessageBox.Show($"Помилка ініціалізації бази даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
