using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Enums;
using System.Windows;
using System.Windows.Controls;

namespace SoftwareEngineering.Client;

public partial class ProjectsView : UserControl
{
    private readonly IProjectService _projectService;
    private readonly IProjectMemberService _projectMemberService;
    private readonly UserSession _userSession;
    private ProjectModel? _selectedProject;
    private bool _isCreateMode = false;

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

            // Редагувати, видаляти проєкт і додавати співвиконавців може лише власник
            var isOwner = selected.OwnerId == (_userSession.CurrentUser?.Id ?? Guid.Empty);
            var ownerOnly = isOwner ? Visibility.Visible : Visibility.Collapsed;

            TxtProjectName.IsReadOnly = !isOwner;
            BtnTransferOwnership.Visibility = ownerOnly;
            BtnSave.Visibility = ownerOnly;
            BtnDelete.Visibility = ownerOnly;
            CoExecutorPanel.Visibility = ownerOnly;
            TxtCoExecutorEmail.Text = string.Empty;
            MembersPanel.Visibility = Visibility.Visible;
            _ = LoadMembersAsync(selected.Id, isOwner);

            DetailsPanel.Visibility = Visibility.Visible;
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
        BtnTransferOwnership.Visibility = Visibility.Collapsed;
        BtnSave.Visibility = Visibility.Visible;
        BtnDelete.Visibility = Visibility.Collapsed;

        // Співвиконавців можна додати лише до вже створеного проєкту
        CoExecutorPanel.Visibility = Visibility.Collapsed;

        MembersPanel.Visibility = Visibility.Collapsed;

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
        if (_selectedProject == null) return;

        var email = TxtCoExecutorEmail.Text.Trim();
        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;

        // Сервіс сам перевіряє email і права та не кидає винятків — помилка приходить у Result
        var result = await _projectMemberService.AddCoExecutorAsync(_selectedProject.Id, email, currentUserId);

        if (result.IsSuccess)
        {
            MessageBox.Show($"Користувача {email} успішно додано до проєкту!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtCoExecutorEmail.Text = string.Empty;
            await LoadMembersAsync(_selectedProject.Id, true);
        }
        else
        {
            MessageBox.Show(result.Error?.Message ?? "Невідома помилка", "Помилка додавання", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string RoleText(ProjectRole role) => role switch
    {
        ProjectRole.Owner => "Власник",
        ProjectRole.CoExecutor => "Співвиконавець",
        ProjectRole.Reviewer => "Рецензент",
        _ => role.ToString(),
    };

    private async Task LoadMembersAsync(Guid projectId, bool isOwner)
    {
        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
        var result = await _projectMemberService.GetMembersAsync(projectId, currentUserId);

        // Користувач міг вибрати інший проєкт, поки йшов запит
        if (_selectedProject?.Id != projectId)
        {
            return;
        }

        if (result.IsFailure)
        {
            MembersList.ItemsSource = null;
            return;
        }

        MembersList.ItemsSource = result.Value!
            .Select(member => new
            {
                member.UserId,
                member.FullName,
                member.Email,
                Role = RoleText(member.Role),
                RemoveVisibility = isOwner && member.Role != ProjectRole.Owner ? Visibility.Visible : Visibility.Collapsed,
            })
            .ToList();
    }

    private async void BtnRemoveMember_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null || sender is not Button { Tag: Guid memberId })
        {
            return;
        }

        var answer = MessageBox.Show(
            "Видалити учасника з проєкту?\nЙого задачі перейдуть до власника проєкту, а виконавця в задачах буде знято.",
            "Підтвердження",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
        var result = await _projectMemberService.RemoveMemberAsync(_selectedProject.Id, memberId, currentUserId);

        if (result.IsFailure)
        {
            MessageBox.Show(result.Error?.Message ?? "Невідома помилка", "Учасника не видалено", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        await LoadMembersAsync(_selectedProject.Id, true);
    }

    private async void BtnTransferOwnership_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;

        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
        var candidatesResult = await _projectMemberService.GetTransferCandidatesAsync(_selectedProject.Id, currentUserId);

        if (candidatesResult.IsFailure)
        {
            MessageBox.Show(candidatesResult.Error?.Message ?? "Не вдалося отримати список учасників.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var candidates = candidatesResult.Value!;
        if (candidates.Count == 0)
        {
            MessageBox.Show(
                "У проєкті поки немає інших учасників. Спочатку додайте хоча б одного співвиконавця за email.",
                "Немає кандидатів",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new TransferOwnershipWindow(
            _projectMemberService,
            _selectedProject.Id,
            _selectedProject.Name,
            currentUserId,
            candidates)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.IsTransferred)
        {
            var transferredProjectId = _selectedProject.Id;

            await LoadProjectsAsync();

            var updatedProject = LstProjects.Items
                .OfType<ProjectModel>()
                .FirstOrDefault(p => p.Id == transferredProjectId);

            if (updatedProject != null)
            {
                LstProjects.SelectedItem = updatedProject;
            }
        }
    }
}
