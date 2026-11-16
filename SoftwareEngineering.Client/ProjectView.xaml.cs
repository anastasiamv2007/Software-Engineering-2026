using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.Client;

public partial class ProjectsView : UserControl
{
    private readonly IProjectService _projectService;
    private readonly IProjectMemberService _projectMemberService;
    private readonly UserSession _userSession;
    private ProjectModel? _selectedProject;
    private bool _isCreateMode = false;
    private bool _isLoadingMembers = false;

    public ProjectsView()
    {
        InitializeComponent();

        var app = (App)Application.Current;
        _projectService = app.GetService<IProjectService>();
        _projectMemberService = app.GetService<IProjectMemberService>();
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

            // Редагувати, видаляти проєкт і керувати учасниками може лише власник
            var isOwner = selected.OwnerId == (_userSession.CurrentUser?.Id ?? Guid.Empty);
            var ownerOnly = isOwner ? Visibility.Visible : Visibility.Collapsed;

            TxtProjectName.IsReadOnly = !isOwner;
            BtnSave.Visibility = ownerOnly;
            BtnDelete.Visibility = ownerOnly;
            CoExecutorPanel.Visibility = ownerOnly;
            TxtCoExecutorEmail.Text = string.Empty;

            MembersPanel.Visibility = Visibility.Visible;
            DetailsPanel.Visibility = Visibility.Visible;

            await LoadProjectMembersAsync(selected.Id, isOwner);
        }
    }

    private async Task LoadProjectMembersAsync(Guid projectId, bool isCurrentUserOwner)
    {
        try
        {
            _isLoadingMembers = true;
            var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
            var members = await _projectMemberService.GetProjectMembersAsync(projectId);

            var roleOptions = new List<RoleOption>
            {
                new(ProjectRole.CoExecutor, "Співвиконавець"),
                new(ProjectRole.Reviewer, "Читач"),
            };

            LstMembers.ItemsSource = members.Select(m =>
            {
                var canEditRole = isCurrentUserOwner && m.Role != ProjectRole.Owner && m.UserId != currentUserId;
                return new MemberRowItem
                {
                    UserId = m.UserId,
                    FullName = m.FullName,
                    Email = m.Email,
                    Role = m.Role,
                    AvailableRoles = roleOptions,
                    ComboVisibility = canEditRole ? Visibility.Visible : Visibility.Collapsed,
                    TextVisibility = canEditRole ? Visibility.Collapsed : Visibility.Visible,
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Помилка завантаження учасників: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoadingMembers = false;
        }
    }

    private async void CmbMemberRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingMembers || _selectedProject == null)
        {
            return;
        }

        if (sender is not ComboBox comboBox || comboBox.DataContext is not MemberRowItem item)
        {
            return;
        }

        if (comboBox.SelectedValue is not ProjectRole newRole || newRole == item.Role)
        {
            return;
        }

        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
        var result = await _projectMemberService.ChangeRoleAsync(_selectedProject.Id, item.UserId, newRole, currentUserId);

        if (result.IsSuccess)
        {
            item.Role = newRole;
        }
        else
        {
            MessageBox.Show(result.Error?.Message ?? "Не вдалося змінити роль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);

            _isLoadingMembers = true;
            comboBox.SelectedValue = item.Role;
            _isLoadingMembers = false;
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

        TxtProjectName.IsReadOnly = false;
        BtnSave.Visibility = Visibility.Visible;
        BtnDelete.Visibility = Visibility.Collapsed;

        // Співвиконавців можна додати лише до вже створеного проєкту
        CoExecutorPanel.Visibility = Visibility.Collapsed;
        MembersPanel.Visibility = Visibility.Collapsed;
        LstMembers.ItemsSource = null;

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
                    OwnerId = currentUserId,
                };

                await _projectService.CreateProjectAsync(createModel, currentUserId);
            }
            else if (_selectedProject != null)
            {
                var updateModel = new UpdateProjectModel
                {
                    Id = _selectedProject.Id,
                    Name = TxtProjectName.Text,
                };

                await _projectService.UpdateProjectAsync(updateModel, currentUserId);
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
                    var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
                    await _projectService.DeleteProjectAsync(_selectedProject.Id, currentUserId);
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
        if (_selectedProject == null)
        {
            return;
        }

        var email = TxtCoExecutorEmail.Text.Trim();
        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;

        // Сервіс сам перевіряє email і права та не кидає винятків — помилка приходить у Result
        var result = await _projectMemberService.AddCoExecutorAsync(_selectedProject.Id, email, currentUserId);

        if (result.IsSuccess)
        {
            MessageBox.Show($"Користувача {email} успішно додано до проєкту!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtCoExecutorEmail.Text = string.Empty;

            var isOwner = _selectedProject.OwnerId == currentUserId;
            await LoadProjectMembersAsync(_selectedProject.Id, isOwner);
        }
        else
        {
            MessageBox.Show(result.Error?.Message ?? "Невідома помилка", "Помилка додавання", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private sealed record RoleOption(ProjectRole Role, string Title);

    private sealed class MemberRowItem
    {
        public Guid UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public ProjectRole Role { get; set; }

        public string RoleDisplay => Role switch
        {
            ProjectRole.Owner => "Власник",
            ProjectRole.CoExecutor => "Співвиконавець",
            ProjectRole.Reviewer => "Читач",
            _ => Role.ToString(),
        };

        public List<RoleOption> AvailableRoles { get; set; } = new();

        public Visibility ComboVisibility { get; set; }

        public Visibility TextVisibility { get; set; }
    }
}