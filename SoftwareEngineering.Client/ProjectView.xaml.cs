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
    private readonly ITaskService _taskService;
    private readonly UserSession _userSession;

    private ProjectModel? _selectedProject;
    private bool _isCreateMode = false;
    private bool _isLoading = false;

    public ProjectsView()
    {
        InitializeComponent();

        var app = (App)Application.Current;
        _projectService = app.GetService<IProjectService>();
        _taskService = app.GetService<ITaskService>();
        _userSession = app.GetService<UserSession>();

        IsVisibleChanged += ProjectsView_IsVisibleChanged;
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (LstProjects.ItemsSource == null)
        {
            await LoadProjectsAsync();
        }
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
        if (_isLoading) return;

        _isLoading = true;
        try
        {
            var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
            var projects = await _projectService.GetUserProjectsAsync(currentUserId);

            // Тимчасово відключаємо SelectionChanged, щоб встановлення ItemsSource не викликало каскадний запит в DbContext
            LstProjects.SelectionChanged -= LstProjects_SelectionChanged;
            LstProjects.ItemsSource = projects.ToList();
            LstProjects.SelectedItem = null;
            LstProjects.SelectionChanged += LstProjects_SelectionChanged;

            DetailsPanel.Visibility = Visibility.Collapsed;
            _selectedProject = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження проєктів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadTasksAsync(Guid projectId)
    {
        try
        {
            var tasks = await _taskService.GetTasksByProjectIdAsync(projectId);
            LstTasks.ItemsSource = tasks.ToList();
            TxtTasksCount.Text = tasks.Count().ToString();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження завдань: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LstProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            TaskSection.Visibility = Visibility.Visible;
            DetailsPanel.Visibility = Visibility.Visible;

            await LoadTasksAsync(selected.Id);
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
        TaskSection.Visibility = Visibility.Collapsed;
        DetailsPanel.Visibility = Visibility.Visible;

        TxtProjectName.Focus();
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

    private async void BtnCreateTask_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;

        if (string.IsNullOrWhiteSpace(TxtNewTaskTitle.Text))
        {
            MessageBox.Show("Будь ласка, введіть назву завдання.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;

            var taskModel = new TaskModel
            {
                ProjectId = _selectedProject.Id,
                CreatorId = currentUserId,
                Title = TxtNewTaskTitle.Text.Trim(),
                Description = string.Empty,
                Deadline = DateTime.UtcNow.AddDays(1),
                Priority = SoftwareEngineering.DAL.Enums.TaskPriority.Medium
            };

            await _taskService.CreateAsync(taskModel);

            TxtNewTaskTitle.Text = string.Empty;
            await LoadTasksAsync(_selectedProject.Id);
        }
        catch (Exception ex)
        {
            string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            MessageBox.Show($"Справжня помилка бази даних:\n\n{trueError}", "Деталі помилки", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid taskId)
        {
            var result = MessageBox.Show("Ви дійсно бажаєте видалити це завдання?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
                    await _taskService.DeleteTaskAsync(taskId, currentUserId);

                    if (_selectedProject != null)
                    {
                        await LoadTasksAsync(_selectedProject.Id);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка видалення завдання: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}