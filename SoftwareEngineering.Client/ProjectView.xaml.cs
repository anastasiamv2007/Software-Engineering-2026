using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
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
        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
        var projects = await _projectService.GetUserProjectsAsync(currentUserId);
        if (projects.ShowIfFailed("Не вдалося завантажити проєкти"))
        {
            return;
        }

        LstProjects.ItemsSource = projects.Value!;
        DetailsPanel.Visibility = Visibility.Collapsed;
        _selectedProject = null;
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

            DetailsPanel.Visibility = Visibility.Visible;
            await LoadMembersAsync(selected.Id, isOwner);
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
        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;

        Result result;
        if (_isCreateMode)
        {
            result = await _projectService.CreateProjectAsync(
                new CreateProjectModel { Name = TxtProjectName.Text, OwnerId = currentUserId },
                currentUserId);
        }
        else if (_selectedProject != null)
        {
            result = await _projectService.UpdateProjectAsync(
                new UpdateProjectModel { Id = _selectedProject.Id, Name = TxtProjectName.Text },
                currentUserId);
        }
        else
        {
            return;
        }

        if (result.ShowIfFailed("Проєкт не збережено"))
        {
            return;
        }

        await LoadProjectsAsync();
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject != null)
        {
            var result = MessageBox.Show($"Ви дійсно бажаєте видалити проєкт \"{_selectedProject.Name}\"?",
                                         "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
                var deleted = await _projectService.DeleteProjectAsync(_selectedProject.Id, currentUserId);
                if (deleted.ShowIfFailed("Проєкт не видалено"))
                {
                    return;
                }

                await LoadProjectsAsync();
            }
        }
    }

    private async void BtnAddCoExecutor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;

        var email = TxtCoExecutorEmail.Text.Trim();
        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;

        // Сервіс сам перевіряє email і права — відмова приходить у Result
        var result = await _projectMemberService.AddCoExecutorAsync(_selectedProject.Id, email, currentUserId);

        if (result.ShowIfFailed("Співвиконавця не додано"))
        {
            return;
        }

        MessageBox.Show($"Користувача {email} успішно додано до проєкту!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        TxtCoExecutorEmail.Text = string.Empty;
        await LoadMembersAsync(_selectedProject.Id, true);
    }

    private static string RoleText(ProjectRole role) => role switch
    {
        ProjectRole.Owner => "Власник",
        ProjectRole.CoExecutor => "Співвиконавець",
        ProjectRole.Reviewer => "Читач",
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
            .Select(member =>
            {
                // Власника не видаляють і не змінюють йому роль — для цього є «Передати власність»
                var canManage = isOwner && member.Role != ProjectRole.Owner;
                return new MemberRow
                {
                    UserId = member.UserId,
                    FullName = member.FullName,
                    Email = member.Email,
                    Role = member.Role,
                    RemoveVisibility = canManage ? Visibility.Visible : Visibility.Collapsed,
                    RoleComboVisibility = canManage ? Visibility.Visible : Visibility.Collapsed,
                    RoleTextVisibility = canManage ? Visibility.Collapsed : Visibility.Visible,
                };
            })
            .ToList();
    }

    private async void CmbMemberRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged спрацьовує і під час побудови списку — реагуємо лише на справжню зміну
        if (_selectedProject == null
            || sender is not ComboBox { DataContext: MemberRow row, SelectedValue: ProjectRole newRole } comboBox
            || newRole == row.Role)
        {
            return;
        }

        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
        var result = await _projectMemberService.ChangeRoleAsync(_selectedProject.Id, row.UserId, newRole, currentUserId);

        if (result.IsSuccess)
        {
            row.Role = newRole;
            return;
        }

        MessageBox.Show(result.Error?.Message ?? "Не вдалося змінити роль.", "Роль не змінено", MessageBoxButton.OK, MessageBoxImage.Warning);

        // Повертаємо попереднє значення (повторний SelectionChanged відсіється перевіркою newRole == row.Role)
        comboBox.SelectedValue = row.Role;
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

        // Список перечитуємо в будь-якому разі: учасника могли вже прибрати з іншого ПК
        result.ShowIfFailed("Учасника не видалено");
        await LoadMembersAsync(_selectedProject.Id, true);
    }

    private async void BtnTransferOwnership_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;

        var currentUserId = _userSession.CurrentUser?.Id ?? Guid.Empty;
        var candidatesResult = await _projectMemberService.GetTransferCandidatesAsync(_selectedProject.Id, currentUserId);

        if (candidatesResult.ShowIfFailed("Не вдалося отримати список учасників"))
        {
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

    private sealed record RoleOption(ProjectRole Role, string Title);

    /// <summary>
    /// Рядок списку учасників: роль можна змінити у випадаючому списку (лише Owner).
    /// </summary>
    private sealed class MemberRow
    {
        public static IReadOnlyList<RoleOption> AssignableRoles { get; } = new[]
        {
            new RoleOption(ProjectRole.CoExecutor, RoleText(ProjectRole.CoExecutor)),
            new RoleOption(ProjectRole.Reviewer, RoleText(ProjectRole.Reviewer)),
        };

        public Guid UserId { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public ProjectRole Role { get; set; }

        public string RoleTitle => RoleText(this.Role);

        public IReadOnlyList<RoleOption> RoleOptions => AssignableRoles;

        public Visibility RemoveVisibility { get; init; }

        public Visibility RoleComboVisibility { get; init; }

        public Visibility RoleTextVisibility { get; init; }
    }
}
