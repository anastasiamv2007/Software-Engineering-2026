using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;

namespace SoftwareEngineering.Client;

public partial class ProjectsView : UserControl
{
    private readonly IProjectService _projectService;
    private readonly ProjectMemberService _projectMemberService;
    private readonly UserSession _userSession;
    private ProjectModel? _selectedProject;
    private bool _isCreateMode = false;

    public ProjectsView()
    {
        InitializeComponent();

        var app = (App)Application.Current;
        _projectService = app.GetService<IProjectService>();
        _projectMemberService = app.GetService<ProjectMemberService>();
        _userSession = app.GetService<UserSession>();

        // Оновлюємо список кожного разу, коли вкладка "Пул проєктів" стає видимою
        IsVisibleChanged += ProjectsView_IsVisibleChanged;
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProjectsAsync();
    }

    private async void ProjectsView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            await LoadProjectsAsync();
        }
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
            var projects = await _projectService.GetUserProjectsAsync(currentUserId);

            LstProjects.ItemsSource = projects.ToList();
            DetailsPanel.Visibility = Visibility.Collapsed;
            _selectedProject = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження проєктів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LstProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstProjects.SelectedItem is ProjectModel selected)
        {
            _selectedProject = selected;
            _isCreateMode = false;

            TxtHeader.Text = "Редагування проєкту";
            TxtProjectName.Text = selected.Name;
            TxtTasksCount.Text = selected.TasksCount.ToString();
            TxtCreatedAt.Text = selected.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

            BtnDelete.Visibility = Visibility.Visible;
            DetailsPanel.Visibility = Visibility.Visible;

            // ДОДАНО: Показуємо поле додавання співвиконавця та очищаємо його
            CoExecutorPanel.Visibility = Visibility.Visible;
            TxtCoExecutorEmail.Text = string.Empty;
        }
    }

    private void BtnCreateNew_Click(object sender, RoutedEventArgs e)
    {
        LstProjects.SelectedItem = null;
        _selectedProject = null;
        _isCreateMode = true;

        TxtHeader.Text = "Створення нового проєкту";
        TxtProjectName.Text = string.Empty;
        TxtTasksCount.Text = "0";
        TxtCreatedAt.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        BtnDelete.Visibility = Visibility.Collapsed;
        DetailsPanel.Visibility = Visibility.Visible;
        TxtProjectName.Focus();

        // ДОДАНО: Ховаємо панель співвиконавця, бо проєкт ще не створено
        CoExecutorPanel.Visibility = Visibility.Collapsed;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;

            if (_isCreateMode)
            {
                var createModel = new CreateProjectModel
                {
                    Name = TxtProjectName.Text,
                    OwnerId = currentUserId
                };

                await _projectService.CreateProjectAsync(createModel, currentUserId);
            }
            else if (_selectedProject != null)
            {
                var updateModel = new UpdateProjectModel
                {
                    Id = _selectedProject.Id,
                    Name = TxtProjectName.Text
                };

                await _projectService.UpdateProjectAsync(updateModel);
            }

            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject != null)
        {
            var result = MessageBox.Show($"Ви дійсно бажаєте видалити проєкт \"{_selectedProject.Name}\"?",
                                         "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _projectService.DeleteProjectAsync(_selectedProject.Id);
                    await LoadProjectsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
    private async void BtnAddCoExecutor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;

        var email = TxtCoExecutorEmail.Text.Trim();
        if (string.IsNullOrEmpty(email))
        {
            MessageBox.Show("Будь ласка, введіть email користувача.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;

        try
        {
            // Викликаємо нашу нову бізнес-логіку з Result
            var result = await _projectMemberService.AddCoExecutorAsync(_selectedProject.Id, email, currentUserId);

            if (result.IsSuccess)
            {
                MessageBox.Show($"Користувача {email} успішно додано до проєкту!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtCoExecutorEmail.Text = string.Empty; // Очищаємо поле
            }
            else
            {
                // Якщо є помилка, показуємо повідомлення, яке повернув наш сервіс
                MessageBox.Show(result.Error?.Message ?? "Невідома помилка", "Помилка додавання", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Критична помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}