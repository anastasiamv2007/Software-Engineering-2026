using Serilog;
using SoftwareEngineering.BLL.Exceptions;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Enums;
using System.Windows;
using System.Windows.Controls;

namespace SoftwareEngineering.Client
{
    public partial class MainWindow : Window
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly IUserSession userSession;
        private IReadOnlyList<TaskModel> allTasks = new List<TaskModel>();
        private TaskModel? currentSelectedTask;

        public MainWindow(ITaskService taskService, IProjectService projectService, IUserSession userSession)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));

            // Поточний користувач береться із сесії, а не вводиться вручну
            UserNameText.Text = userSession.CurrentUser?.FullName ?? "Користувач";
            ProgUserIdTextBox.Text = CurrentUserId.ToString();
            ProgUserIdTextBox.IsReadOnly = true;
            DelUserIdTextBox.Text = CurrentUserId.ToString();
            DelUserIdTextBox.IsReadOnly = true;

            DeadlineDatePicker.SelectedDate = DateTime.Now.AddDays(1);
            Loaded += async (_, _) => await LoadTasksAsync();
        }

        private Guid CurrentUserId => userSession.CurrentUserId ?? Guid.Empty;

        // Готові обробники для кнопок виходу — прив'яжіть у XAML через Click="SignOut_Click" / Click="SignOutAndDisconnect_Click"
        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).SignOut();
        }

        private void SignOutAndDisconnect_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).SignOutAndDisconnect();
        }

        // DatePicker повертає лише дату — якщо день не змінився, лишаємо початковий дедлайн разом із часом
        private static DateTime? KeepTimeIfSameDay(DateTime? original, DateTime? picked)
        {
            return original.HasValue && picked.HasValue && original.Value.ToLocalTime().Date == picked.Value.Date
                ? original
                : picked;
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: string page })
            {
                ShowPage(page);
            }
        }

        private void ShowPage(string page)
        {
            foreach (FrameworkElement element in PagesHost.Children)
            {
                element.Visibility = element.Name == $"Page_{page}" ? Visibility.Visible : Visibility.Collapsed;
            }

            if (page == "Projects")
            {
                var projectsView = Page_Projects.Children.OfType<ProjectsView>().FirstOrDefault();
            }
        }

        private void CancelToTasks_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Tasks");
            NavTasks.IsChecked = true;
        }

        private async Task LoadTasksAsync()
        {
            try
            {
                allTasks = await taskService.GetAllAsync();
                ApplyFilter();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося завантажити завдання: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            var query = SearchBox.Text?.Trim() ?? string.Empty;
            TasksDataGrid.ItemsSource = string.IsNullOrEmpty(query)
              ? allTasks
              : allTasks.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (allTasks != null) ApplyFilter();
        }

        private void UpdateStats()
        {
            var now = DateTime.UtcNow;
            var total = allTasks.Count;
            var done = allTasks.Count(t => t.Status == TaskItemStatus.Done);
            var inProgress = allTasks.Count(t => t.Status == TaskItemStatus.InProgress);
            var overdue = allTasks.Count(t => t.Status != TaskItemStatus.Done && t.Deadline.HasValue && t.Deadline.Value.ToUniversalTime() < now);
            var active = total - done;

            StatTotal.Text = total.ToString();
            StatInProgress.Text = inProgress.ToString();
            StatDone.Text = done.ToString();
            StatOverdue.Text = overdue.ToString();

            UpcomingList.ItemsSource = allTasks
              .Where(t => t.Status != TaskItemStatus.Done)
              .OrderBy(t => t.Deadline)
              .Take(5)
              .ToList();

            AnTotal.Text = $"Загальна кількість завдань: {total}";
            AnDone.Text = $"Виконано: {done}";
            AnActive.Text = $"Невиконано: {active}";
            AnOverdue.Text = $"Протерміновано: {overdue}";
            AnProgress.Value = total == 0 ? 0 : done * 100.0 / total;
        }

        private void GoToCreate_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Create");
            NavCreate.IsChecked = true;
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var priority = TaskPriority.Medium;
                if (PriorityComboBox.SelectedItem is ComboBoxItem { Tag: TaskPriority parsed })
                {
                    priority = parsed;
                }

                var project = await projectService.GetOrCreateByNameAsync(ProjectTextBox.Text ?? string.Empty, CurrentUserId);

                await taskService.CreateAsync(new TaskModel
                {
                    ProjectId = project.Id,
                    OwnerId = CurrentUserId,
                    Title = TitleTextBox.Text?.Trim() ?? string.Empty,
                    Description = DescriptionTextBox.Text?.Trim() ?? string.Empty,
                    Deadline = DeadlineDatePicker.SelectedDate ?? DateTime.Now,
                    Priority = priority,
                    Status = TaskItemStatus.Todo,
                });

                MessageBox.Show("Завдання успішно створено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                TitleTextBox.Clear();
                DescriptionTextBox.Clear();
                await LoadTasksAsync();
                ShowPage("Tasks");
                NavTasks.IsChecked = true;
            }
            catch (ValidationException ex)
            {
                MessageBox.Show(ex.Message, "Помилка валідації", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сталася помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GoToDetails_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is TaskModel selected)
            {
                try
                {
                    var task = await taskService.GetByIdAsync(selected.Id);

                    DetailsTitleText.Text = task.Title;
                    DetailsStatusText.Text = $"Статус: {task.Status} | Пріоритет: {task.Priority}";
                    DetailsDescriptionText.Text = string.IsNullOrWhiteSpace(task.Description) ? "Немає опису" : task.Description;
                    DetailsDeadlineText.Text = task.Deadline.HasValue ? $"Дедлайн: {task.Deadline.Value:dd.MM.yyyy HH:mm}" : "Дедлайн: Не вказано";

                    ShowPage("Details");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Виберіть завдання зі списку.", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void EditTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is TaskModel selectedTask)
            {
                var editWindow = new EditTaskWindow(taskService, selectedTask, CurrentUserId)
                {
                    Owner = this
                };

                if (editWindow.ShowDialog() == true)
                {
                    await LoadTasksAsync();
                }
            }
            else
            {
                MessageBox.Show("Виберіть завдання для редагування.", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GoToEdit_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is TaskModel selected)
            {
                currentSelectedTask = selected;

                EditTitleTextBox.Text = selected.Title;
                EditDescriptionTextBox.Text = selected.Description;
                EditDeadlineDatePicker.SelectedDate = selected.Deadline;

                foreach (ComboBoxItem item in EditPriorityComboBox.Items)
                {
                    if (item.Tag is TaskPriority priority && priority == selected.Priority)
                    {
                        EditPriorityComboBox.SelectedItem = item;
                        break;
                    }
                }

                ShowPage("Edit");
            }
            else
            {
                MessageBox.Show("Виберіть завдання зі списку.", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void SaveEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSelectedTask == null) return;

            try
            {
                var priority = TaskPriority.Medium;
                if (EditPriorityComboBox.SelectedItem is ComboBoxItem selectedItem &&
                  selectedItem.Tag is TaskPriority parsedPriority)
                {
                    priority = parsedPriority;
                }

                currentSelectedTask.Title = EditTitleTextBox.Text?.Trim() ?? string.Empty;
                currentSelectedTask.Description = EditDescriptionTextBox.Text?.Trim() ?? string.Empty;
                currentSelectedTask.Deadline = KeepTimeIfSameDay(currentSelectedTask.Deadline, EditDeadlineDatePicker.SelectedDate);
                currentSelectedTask.Priority = priority;

                await taskService.UpdateAsync(currentSelectedTask, CurrentUserId);

                Log.Information("Оновлено завдання ID: {TaskId}", currentSelectedTask.Id);
                MessageBox.Show("Завдання успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadTasksAsync();
                ShowPage("Tasks");
                NavTasks.IsChecked = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сталася помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoToUpdateProgress_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is TaskModel selected)
            {
                ProgTaskIdTextBox.Text = selected.Id.ToString();
            }
            ShowPage("UpdateProgress");
        }

        private async void SaveProgressButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ = Guid.TryParse(ProgTaskIdTextBox.Text?.Trim(), out var taskId);
                var userId = CurrentUserId;

                var status = TaskItemStatus.InProgress;
                if (ProgStatusComboBox.SelectedItem is ComboBoxItem selectedItem &&
                  selectedItem.Tag is TaskItemStatus parsedStatus)
                {
                    status = parsedStatus;
                }

                await taskService.UpdateProgressAsync(taskId, status, userId);

                MessageBox.Show("Прогрес успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadTasksAsync();
                ShowPage("Tasks");
                NavTasks.IsChecked = true;
            }
            catch (ValidationException ex)
            {
                MessageBox.Show(ex.Message, "Помилка валідації", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сталася помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoToDelete_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is TaskModel selected)
            {
                DelTaskIdTextBox.Text = selected.Id.ToString();
            }
            DelStatusText.Text = "Очікування підтвердження...";
            ShowPage("Delete");
        }

        private async void ConfirmDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DelStatusText.Text = "Виконується запит...";

            if (!Guid.TryParse(DelTaskIdTextBox.Text.Trim(), out Guid taskId))
            {
                DelStatusText.Text = "Помилка: Невалідний Task ID.";
                return;
            }

            var userId = CurrentUserId;

            try
            {
                var model = new TaskModel { Id = taskId };
                await taskService.DeleteTaskByOwnerAsync(model, userId);

                DelStatusText.Text = $"[УСПІХ]: Завдання {taskId} успішно видалено!";
                MessageBox.Show("Завдання успішно видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadTasksAsync();
                ShowPage("Tasks");
                NavTasks.IsChecked = true;
            }
            catch (ValidationException valEx)
            {
                DelStatusText.Text = $"[ВІДХИЛЕНО]: {valEx.Message}";
            }
            catch (Exception ex)
            {
                DelStatusText.Text = $"[ПОМИЛКА]: {ex.Message}";
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Ви дійсно хочете вийти з аккаунта?", "Вихід", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // Логування події виходу в Seq
                Log.Information("Користувач вийшов із системи о {Time}", DateTime.UtcNow);

                var loginWindow = ((App)Application.Current).GetService<LoginWindow>();
                loginWindow.Show();
                this.Close();
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Ви дійсно хочете розірвати підключення до бази даних?", "Змінити базу", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Log.Information("Користувач розірвав з'єднання з базою даних о {Time}", DateTime.UtcNow);
                ((App)Application.Current).SignOutAndDisconnect();
            }
        }

        private void RegisterNewAccountButton_Click(object sender, RoutedEventArgs e)
        {
            // Логування переходу до реєстрації в Seq
            Log.Information("Користувач ініціював створення нового аккаунта.");

            var loginWindow = ((App)Application.Current).GetService<LoginWindow>();
            loginWindow.Show();
            this.Close();
        }

    }

}