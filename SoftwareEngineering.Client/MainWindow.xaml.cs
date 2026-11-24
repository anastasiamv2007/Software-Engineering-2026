using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Enums;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace SoftwareEngineering.Client
{
    public partial class MainWindow : Window
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly IUserSession userSession;
        private IReadOnlyList<TaskModel> allTasks = new List<TaskModel>();
        private TaskModel? currentSelectedTask;
        private readonly ICategoryService categoryService;
        private readonly ISubtaskService subtaskService;
        private readonly ICommentService commentService;
        private readonly IReviewService reviewService;
        private Guid? detailsTaskId;
        private CategoryModel? selectedCategory;
        private string selectedCategoryColor = CategoryPalette[0];
        private readonly IStatisticsService statisticsService;


        private static readonly string[] CategoryPalette =
        {
            "#3B82F6", "#EC4899", "#8B5CF6", "#10B981", "#F59E0B", "#EF4444", "#06B6D4", "#6366F1",
        };

        public MainWindow(ITaskService taskService, IProjectService projectService, IUserSession userSession, ICategoryService categoryService, ISubtaskService subtaskService, ICommentService commentService, IStatisticsService statisticsService, IReviewService reviewService)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
            this.categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            this.subtaskService = subtaskService ?? throw new ArgumentNullException(nameof(subtaskService));
            this.commentService = commentService ?? throw new ArgumentNullException(nameof(commentService));
            this.reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
            this.statisticsService = statisticsService ?? throw new ArgumentNullException(nameof(statisticsService));

            BuildCategoryPalette();
            StartCategoryCreateMode();

            // Поточний користувач береться із сесії, а не вводиться вручну
            UserNameText.Text = userSession.CurrentUser?.FullName ?? "Користувач";
            UserInitialsText.Text = GetInitials(userSession.CurrentUser?.FullName);
            ProgUserIdTextBox.Text = CurrentUserId.ToString();
            ProgUserIdTextBox.IsReadOnly = true;
            DelUserIdTextBox.Text = CurrentUserId.ToString();
            DelUserIdTextBox.IsReadOnly = true;

            DeadlineDatePicker.SelectedDate = DateTime.Now.AddDays(1);
            Loaded += async (_, _) =>
            {
                // Категорії потрібні для фільтра на сторінці завдань
                await LoadCategoriesAsync();
                await LoadTasksAsync();
            };
        }

        private void TasksDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Подвійний клік по заголовку чи смузі прокрутки не має відкривати деталі вибраного раніше рядка
            var row = ItemsControl.ContainerFromElement(TasksDataGrid, (DependencyObject)e.OriginalSource) as DataGridRow;
            if (row?.Item is TaskModel)
            {
                // Та сама логіка, що й у кнопки "Деталі"
                GoToDetails_Click(sender, new RoutedEventArgs());
            }
        }

        private Guid CurrentUserId => userSession.CurrentUserId ?? Guid.Empty;

        // "Іван Петренко" -> "ІП"; логування виходу вже є всередині App.SignOut / SignOutAndDisconnect
        private static string GetInitials(string? fullName)
        {
            var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Ви дійсно хочете вийти з облікового запису?\nЗбережений вхід на цьому пристрої буде скасовано.",
                "Вихід",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ((App)Application.Current).SignOut();
            }
        }

        private void SignOutAndDisconnect_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Відключитися від цієї бази та обрати іншу?\nЯкщо вхід збережено, для цієї бази логін наступного разу не знадобиться.",
                "Змінити базу",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ((App)Application.Current).SignOutAndDisconnect();
            }
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

        // async void: помилка завантаження сторінки дійде до глобального обробника в App, а не загубиться
        private async void ShowPage(string page)
        {
            foreach (FrameworkElement element in PagesHost.Children)
            {
                element.Visibility = element.Name == $"Page_{page}" ? Visibility.Visible : Visibility.Collapsed;
            }

            // Список категорій оновлюємо щоразу, коли відкривається вкладка
            if (page == "Categories")
            {
                await LoadCategoriesAsync();
            }

            if (page == "Projects")
            {
                var projectsView = Page_Projects.Children.OfType<ProjectsView>().FirstOrDefault();
            }

            if (page == "Review")
            {
                await LoadReviewQueueAsync();
            }

            if (page == "Create")
            {
                await LoadProjectChoicesAsync();
            }

            if (page == "Calendar")
            {
                RenderCalendar();
            }
        }

        private void CancelToTasks_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Tasks");
            NavTasks.IsChecked = true;
        }

        private async Task LoadTasksAsync()
        {
            // Лише завдання проєктів, де користувач — власник або співвиконавець
            var result = await taskService.GetUserTasksAsync(CurrentUserId);
            if (result.ShowIfFailed("Не вдалося завантажити завдання"))
            {
                return;
            }

            allTasks = result.Value!;
            ApplyFilter();
            if (Page_Calendar.Visibility == Visibility.Visible)
            {
                RenderCalendar();
            }

            await UpdateStatsAsync(); ;
        }

        private void ApplyFilter()
        {
            if (allTasks == null) return;

            var criteria = new TaskFilterCriteria
            {
                Status = GetSelectedEnum<TaskItemStatus>(FilterStatusComboBox),
                Priority = GetSelectedEnum<TaskPriority>(FilterPriorityComboBox),
                CategoryId = GetSelectedCategoryId(FilterCategoryComboBox),
                OnlyOverdue = FilterOverdueCheckBox.IsChecked ?? false,
                DeadlineFrom = FilterDeadlineFromPicker.SelectedDate,
                DeadlineTo = FilterDeadlineToPicker.SelectedDate,
                SortBy = GetSelectedEnum<TaskSortOption>(SortComboBox) ?? TaskSortOption.ByDeadlineAsc,
            };

            var filterResult = TaskFilter.Apply(allTasks, criteria);
            if (filterResult.IsFailure)
            {
                // Помилку видно під панеллю фільтрів, а не у вікні: фільтр змінюють часто, MessageBox заважав би
                FilterErrorText.Text = filterResult.Error!.Message;
                FilterErrorText.Visibility = Visibility.Visible;
                return;
            }

            FilterErrorText.Visibility = Visibility.Collapsed;

            IReadOnlyList<TaskModel> filteredList = filterResult.Value!;

            // Пошук за назвою працює поверх фільтрів
            var query = SearchBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(query))
            {
                filteredList = filteredList.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            TasksDataGrid.ItemsSource = filteredList;
        }

        private static T? GetSelectedEnum<T>(ComboBox comboBox) where T : struct
        {
            if (comboBox?.SelectedItem is ComboBoxItem { Tag: not null } selectedItem &&
                Enum.TryParse(selectedItem.Tag.ToString(), out T result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Оновлює списки категорій у фільтрі та формах завдання, зберігаючи поточний вибір.
        /// </summary>
        private void RefreshCategoryLists(IReadOnlyList<CategoryModel> categories)
        {
            FillCategoryComboBox(FilterCategoryComboBox, categories, "Всі");
            FillCategoryComboBox(CategoryComboBox, categories, "Без категорії");
            FillCategoryComboBox(EditCategoryComboBox, categories, "Без категорії");
        }

        private static void FillCategoryComboBox(ComboBox comboBox, IReadOnlyList<CategoryModel> categories, string emptyText)
        {
            var selectedId = GetSelectedCategoryId(comboBox);

            comboBox.Items.Clear();
            comboBox.Items.Add(new ComboBoxItem { Content = emptyText });
            foreach (var category in categories)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal };
                item.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Margin = new Thickness(0, 0, 6, 0),
                    Fill = ToBrush(category.ColorHex),
                    VerticalAlignment = VerticalAlignment.Center,
                });
                item.Children.Add(new TextBlock { Text = category.Name });

                comboBox.Items.Add(new ComboBoxItem { Content = item, Tag = category.Id });
            }

            SelectCategory(comboBox, selectedId);
        }

        private static int? GetSelectedCategoryId(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag as int?;
        }

        // Якщо категорії вже немає у списку (її видалили) — обирається порожній пункт
        private static void SelectCategory(ComboBox comboBox, int? categoryId)
        {
            if (comboBox.Items.Count == 0)
            {
                return;
            }

            comboBox.SelectedItem = comboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag as int? == categoryId) ?? comboBox.Items[0];
        }

        private void OnFilterChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                ApplyFilter();
            }
        }

        private void OnClearFiltersClicked(object sender, RoutedEventArgs e)
        {
            FilterStatusComboBox.SelectedIndex = 0;
            FilterPriorityComboBox.SelectedIndex = 0;
            FilterCategoryComboBox.SelectedIndex = 0;
            SortComboBox.SelectedIndex = 0;
            FilterOverdueCheckBox.IsChecked = false;
            FilterDeadlineFromPicker.SelectedDate = null;
            FilterDeadlineToPicker.SelectedDate = null;
            SearchBox.Text = string.Empty;

            ApplyFilter();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (allTasks != null) ApplyFilter();
        }

        private async Task UpdateStatsAsync()
        {
            var result = await statisticsService.GetStatisticsAsync(CurrentUserId);
            if (result.ShowIfFailed("Не вдалося отримати статистику"))
            {
                return;
            }

            var stats = result.Value!;

            StatTotal.Text = stats.Total.ToString();
            StatInProgress.Text = stats.InProgress.ToString();
            StatDone.Text = stats.Completed.ToString();
            StatOverdue.Text = stats.Overdue.ToString();

            AnTotal.Text = $"Загальна кількість завдань: {stats.Total}";
            AnDone.Text = $"Виконано: {stats.Completed}";
            AnActive.Text = $"Невиконано: {stats.Active}";
            AnOverdue.Text = $"Протерміновано: {stats.Overdue}";
            AnProgress.Value = stats.CompletedPercent;


            UpcomingList.ItemsSource = allTasks
              .Where(t => t.Status != TaskItemStatus.Done)
              .OrderBy(t => t.Deadline)
              .Take(5)
              .ToList();
        }

        private void GoToCreate_Click(object sender, RoutedEventArgs e)
        {
            ShowPage("Create");
            NavCreate.IsChecked = true;
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var priority = TaskPriority.Medium;
            if (PriorityComboBox.SelectedItem is ComboBoxItem { Tag: TaskPriority parsed })
            {
                priority = parsed;
            }

            // Проєкт тільки зі списку: створювати проєкт «на льоту» з назви більше не можна
            if (ProjectComboBox.SelectedItem is not ProjectModel project)
            {
                MessageBox.Show("Оберіть проєкт зі списку.", "Завдання не створено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var created = await taskService.CreateAsync(new TaskModel
            {
                ProjectId = project.Id,
                OwnerId = CurrentUserId,
                Title = TitleTextBox.Text?.Trim() ?? string.Empty,
                Description = DescriptionTextBox.Text?.Trim() ?? string.Empty,
                Deadline = EndOfDay(DeadlineDatePicker.SelectedDate),
                Priority = priority,
                CategoryId = GetSelectedCategoryId(CategoryComboBox),
                Status = TaskItemStatus.Todo,
            });
            if (created.ShowIfFailed("Завдання не створено"))
            {
                return;
            }

            MessageBox.Show($"Завдання {created.Value!.TaskCode} успішно створено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            TitleTextBox.Clear();
            DescriptionTextBox.Clear();
            CategoryComboBox.SelectedIndex = 0;
            await LoadTasksAsync();
            ShowPage("Tasks");
            NavTasks.IsChecked = true;
        }

        private async void GoToDetails_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is TaskModel selected)
            {
                await OpenTaskDetailsAsync(selected.Id);
            }
            else
            {
                MessageBox.Show("Виберіть завдання зі списку.", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void CopyTask_Click(object sender, RoutedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is not TaskModel selected)
            {
                MessageBox.Show("Виберіть завдання, яке потрібно скопіювати.", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new DuplicateTaskWindow(taskService, selected, CurrentUserId) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.CreatedTask is not { } copy)
            {
                return;
            }

            MessageBox.Show($"Створено копію {copy.TaskCode} «{copy.Title}».", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadTasksAsync();
            ShowPage("Tasks");
            NavTasks.IsChecked = true;
            TasksDataGrid.SelectedItem = allTasks.FirstOrDefault(task => task.Id == copy.Id);
        }

        private void ShowTaskDetails(TaskDetailsModel details)
        {
            var task = details.Task;

            DetailsCodeText.Text = $"{task.TaskCode} · {details.ProjectName}";
            DetailsTitleText.Text = task.Title;

            DetailsBadges.Children.Clear();
            DetailsBadges.Children.Add(CreateBadge(StatusText(task.Status), "#DBEAFE", "#1D4ED8"));
            DetailsBadges.Children.Add(CreateBadge($"Пріоритет: {PriorityText(task.Priority)}", "#F1F5F9", "#334155"));
            if (details.IsOverdue)
            {
                DetailsBadges.Children.Add(CreateBadge("Прострочено", "#FEE2E2", "#B91C1C"));
            }

            if (details.CategoryName != null)
            {
                DetailsBadges.Children.Add(CreateBadge(details.CategoryName, details.CategoryColorHex ?? "#E2E8F0", "#FFFFFF"));
            }

            DetailsDescriptionText.Text = string.IsNullOrWhiteSpace(task.Description) ? "Немає опису" : task.Description;


            DetailsMembersList.ItemsSource = details.Members
                .Select(member => new { member.FullName, member.Email, Role = RoleText(member.Role) })
                .ToList();

            DetailsInfoGrid.Children.Clear();
            DetailsInfoGrid.RowDefinitions.Clear();
            AddInfoRow("Проєкт", details.ProjectName);
            AddInfoRow("Власник проєкту", details.ProjectOwnerName);
            AddInfoRow("Власник завдання", $"{details.OwnerName}\n{details.OwnerEmail}");
            if (details.CreatorName != null && details.CreatorName != details.OwnerName)
            {
                AddInfoRow("Автор", details.CreatorName);
            }

            AddInfoRow("Виконавець", details.AssigneeName ?? "Не призначено");
            AddInfoRow("Категорія", details.CategoryName ?? "Без категорії");
            AddInfoRow("Дедлайн", task.Deadline.HasValue ? task.Deadline.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "Не вказано");
            AddInfoRow("Створено", details.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
            AddInfoRow("Ваша роль", details.CurrentUserRole.HasValue ? RoleText(details.CurrentUserRole.Value) : "Автор завдання");
            AddInfoRow("Ваші права", details.CanDeleteOrChangeDeadline
                ? "Редагування, дедлайн, видалення"
                : details.CanEdit ? "Редагування (без дедлайну та видалення)" : "Лише перегляд");

            DetailsEditButton.IsEnabled = details.CanEdit;

            // Копіювати, як і створювати, можуть лише власник проєкту та співвиконавці
            DetailsCopyButton.IsEnabled = details.CurrentUserRole is ProjectRole.Owner or ProjectRole.CoExecutor;

            // Ті самі права, що перевіряє SubtaskService: відмічати може і виконавець, видаляти — автор завдання або власник проєкту
            detailsTaskId = task.Id;
            var canEditSubtasks = details.CanEdit || task.AssigneeId == CurrentUserId;
            DetailsSubtasksList.IsEnabled = canEditSubtasks;
            DetailsAddSubtaskPanel.Visibility = canEditSubtasks ? Visibility.Visible : Visibility.Collapsed;
            DetailsSubtasksList.Tag = details.CanDeleteOrChangeDeadline ? Visibility.Visible : Visibility.Collapsed;
            NewSubtaskTitleTextBox.Clear();
            NewCommentTextBox.Clear();

            var canSubmitForReview =
                (details.CurrentUserRole is ProjectRole.Owner or ProjectRole.CoExecutor || task.AssigneeId == CurrentUserId) &&
                task.Status is TaskItemStatus.Todo or TaskItemStatus.InProgress or TaskItemStatus.NeedsRevision;

            DetailsSubmitReviewButton.Visibility = canSubmitForReview ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddInfoRow(string label, string value)
        {
            var row = DetailsInfoGrid.RowDefinitions.Count;
            DetailsInfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelText = new TextBlock { Text = label, Foreground = ToBrush("#64748B"), Margin = new Thickness(0, 6, 12, 6) };
            var valueText = new TextBlock { Text = value, Foreground = ToBrush("#0F172A"), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
            Grid.SetRow(labelText, row);
            Grid.SetRow(valueText, row);
            Grid.SetColumn(valueText, 1);

            DetailsInfoGrid.Children.Add(labelText);
            DetailsInfoGrid.Children.Add(valueText);
        }

        private static Border CreateBadge(string text, string background, string foreground)
        {
            return new Border
            {
                Background = ToBrush(background),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 8, 4),
                Child = new TextBlock { Text = text, Foreground = ToBrush(foreground), FontSize = 12, FontWeight = FontWeights.SemiBold },
            };
        }

        private static string StatusText(TaskItemStatus status) => status switch
        {
            TaskItemStatus.Todo => "До виконання",
            TaskItemStatus.InProgress => "В процесі",
            TaskItemStatus.InReview => "На перевірці",
            TaskItemStatus.NeedsRevision => "Потребує доопрацювання",
            TaskItemStatus.Done => "Виконано",
            _ => status.ToString(),
        };

        private static string PriorityText(TaskPriority priority) => priority switch
        {
            TaskPriority.High => "високий",
            TaskPriority.Medium => "середній",
            TaskPriority.Low => "низький",
            _ => priority.ToString(),
        };

        private static string RoleText(ProjectRole role) => role switch
        {
            ProjectRole.Owner => "Власник",
            ProjectRole.CoExecutor => "Співвиконавець",
            ProjectRole.Reviewer => "Читач",
            _ => role.ToString(),
        };

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

        private async void GoToEdit_Click(object sender, RoutedEventArgs e)
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

                SelectCategory(EditCategoryComboBox, selected.CategoryId);
                await LoadAssigneeChoicesAsync(selected);


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

            var priority = TaskPriority.Medium;
            if (EditPriorityComboBox.SelectedItem is ComboBoxItem selectedItem &&
              selectedItem.Tag is TaskPriority parsedPriority)
            {
                priority = parsedPriority;
            }

            // Редагуємо копію: якщо сервіс відмовить, у списку лишаться справжні (збережені) значення
            var edited = new TaskModel
            {
                Id = currentSelectedTask.Id,
                ProjectId = currentSelectedTask.ProjectId,
                Title = EditTitleTextBox.Text?.Trim() ?? string.Empty,
                Description = EditDescriptionTextBox.Text?.Trim() ?? string.Empty,
                Deadline = KeepTimeIfSameDay(currentSelectedTask.Deadline, EditDeadlineDatePicker.SelectedDate),
                Priority = priority,
                CategoryId = GetSelectedCategoryId(EditCategoryComboBox),
            };

            var updated = await taskService.UpdateAsync(edited, CurrentUserId);
            if (updated.ShowIfFailed("Завдання не оновлено"))
            {
                return;
            }

            if (EditAssigneeComboBox.IsEnabled)
            {
                var newAssigneeId = (EditAssigneeComboBox.SelectedItem as ComboBoxItem)?.Tag as Guid?;
                if (newAssigneeId != currentSelectedTask.AssigneeId)
                {
                    var assigned = await taskService.AssignAsync(currentSelectedTask.Id, newAssigneeId, CurrentUserId);
                    if (assigned.ShowIfFailed("Зміни збережено, але виконавця не змінено"))
                    {
                        await LoadTasksAsync();
                        return;
                    }
                }
            }

            MessageBox.Show("Завдання успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadTasksAsync();
            ShowPage("Tasks");
            NavTasks.IsChecked = true;
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
            // Невалідний текст id дасть Guid.Empty — сервіс поверне зрозумілу помилку валідації
            _ = Guid.TryParse(ProgTaskIdTextBox.Text?.Trim(), out var taskId);

            var status = TaskItemStatus.InProgress;
            if (ProgStatusComboBox.SelectedItem is ComboBoxItem selectedItem &&
              selectedItem.Tag is TaskItemStatus parsedStatus)
            {
                status = parsedStatus;
            }

            var result = await taskService.UpdateProgressAsync(taskId, status, CurrentUserId);
            if (result.ShowIfFailed("Прогрес не оновлено"))
            {
                return;
            }

            MessageBox.Show("Прогрес успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadTasksAsync();
            ShowPage("Tasks");
            NavTasks.IsChecked = true;
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

            var result = await taskService.DeleteTaskByOwnerAsync(new TaskModel { Id = taskId }, CurrentUserId);
            if (result.IsFailure)
            {
                // На цій сторінці результат показується в рядку статусу, а не окремим вікном
                var prefix = result.Error!.Type == ErrorType.Unexpected ? "ПОМИЛКА" : "ВІДХИЛЕНО";
                DelStatusText.Text = $"[{prefix}]: {result.Error.Message}";
                return;
            }

            DelStatusText.Text = $"[УСПІХ]: Завдання {result.Value!.TaskCode} успішно видалено!";
            MessageBox.Show("Завдання успішно видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadTasksAsync();
            ShowPage("Tasks");
            NavTasks.IsChecked = true;
        }

        // ---------- Категорії ----------
        private static Brush ToBrush(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }

        private async Task LoadCategoriesAsync()
        {
            var result = await categoryService.GetAllAsync();
            if (result.IsFailure)
            {
                ShowCategoryError(result.Error!.Message);
                return;
            }

            CatList.ItemsSource = result.Value;
            RefreshCategoryLists(result.Value!);
        }

        /// <summary>
        /// Після зміни категорії оновлюємо і завдання: у них показуються назва та колір категорії.
        /// </summary>
        private async Task ReloadAfterCategoryChangeAsync()
        {
            StartCategoryCreateMode();
            await LoadCategoriesAsync();
            await LoadTasksAsync();
        }

        private void BuildCategoryPalette()
        {
            foreach (var hex in CategoryPalette)
            {
                var button = new Button
                {
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = ToBrush(hex),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = hex,
                };
                button.Click += (_, _) => SelectCategoryColor(hex);
                CatPalette.Children.Add(button);
            }
        }

        private void SelectCategoryColor(string hex)
        {
            selectedCategoryColor = hex;
            CatPreviewDot.Fill = ToBrush(hex);

            foreach (Button button in CatPalette.Children)
            {
                button.BorderThickness = new Thickness((string)button.Tag == hex ? 3 : 0);
                button.BorderBrush = Brushes.Black;
            }
        }

        private void StartCategoryCreateMode()
        {
            selectedCategory = null;
            CatList.SelectedItem = null;
            CatHeader.Text = "Створити категорію";
            CatSaveButton.Content = "Додати категорію";
            CatDeleteButton.Visibility = Visibility.Collapsed;
            CatName.Text = string.Empty;
            CatError.Visibility = Visibility.Collapsed;
            SelectCategoryColor(CategoryPalette[0]);
        }

        private void ShowCategoryError(string message)
        {
            CatError.Text = message;
            CatError.Visibility = Visibility.Visible;
        }

        private void CatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CatList.SelectedItem is not CategoryModel category)
            {
                return;
            }

            selectedCategory = category;
            CatHeader.Text = "Редагування категорії";
            CatSaveButton.Content = "Зберегти зміни";
            CatDeleteButton.Visibility = Visibility.Visible;
            CatName.Text = category.Name;
            CatError.Visibility = Visibility.Collapsed;
            SelectCategoryColor(category.ColorHex);
        }

        private void CatName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CatPreviewText != null)
            {
                CatPreviewText.Text = string.IsNullOrWhiteSpace(CatName.Text) ? "Поки що без назви" : CatName.Text.Trim();
            }
        }

        private void CatCancel_Click(object sender, RoutedEventArgs e)
        {
            StartCategoryCreateMode();
        }

        private async void CatSave_Click(object sender, RoutedEventArgs e)
        {
            CatError.Visibility = Visibility.Collapsed;

            Result result = selectedCategory is null
                ? await categoryService.CreateAsync(new CreateCategoryModel { Name = CatName.Text, ColorHex = selectedCategoryColor })
                : await categoryService.UpdateAsync(new UpdateCategoryModel { Id = selectedCategory.Id, Name = CatName.Text, ColorHex = selectedCategoryColor });

            if (result.IsFailure)
            {
                ShowCategoryError(result.Error!.Message);
                return;
            }

            await ReloadAfterCategoryChangeAsync();
        }

        private async void CatDelete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedCategory is null)
            {
                return;
            }

            var answer = MessageBox.Show(
                $"Видалити категорію \"{selectedCategory.Name}\"? У завданнях категорію буде скинуто.",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await categoryService.DeleteAsync(selectedCategory.Id);
            if (result.IsFailure)
            {
                ShowCategoryError(result.Error!.Message);
                return;
            }

            await ReloadAfterCategoryChangeAsync();
        }


        private async Task LoadAssigneeChoicesAsync(TaskModel task)
        {
            EditAssigneeComboBox.Items.Clear();
            EditAssigneeComboBox.Items.Add(new ComboBoxItem { Content = "Не призначено" });
            EditAssigneeComboBox.SelectedIndex = 0;
            EditAssigneeComboBox.IsEnabled = false;

            var result = await taskService.GetAssigneeCandidatesAsync(task.Id, CurrentUserId);
            if (result.IsFailure)
            {
                return;
            }

            foreach (var option in result.Value!)
            {
                EditAssigneeComboBox.Items.Add(new ComboBoxItem { Content = option.FullName, Tag = option.UserId });
            }

            EditAssigneeComboBox.SelectedItem = EditAssigneeComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag as Guid? == task.AssigneeId) ?? EditAssigneeComboBox.Items[0];
            EditAssigneeComboBox.IsEnabled = true;
        }

        // ---------- Коментарі ----------
        private async Task LoadCommentsAsync()
        {
            if (detailsTaskId is not { } taskId)
            {
                return;
            }

            var result = await commentService.GetByTaskAsync(taskId, CurrentUserId);
            if (result.IsFailure)
            {
                DetailsCommentsList.ItemsSource = null;
                DetailsCommentsText.Text = $"Коментарі: {result.Error!.Message}";
                return;
            }

            var comments = result.Value!;
            DetailsCommentsText.Text = comments.Count == 0 ? "Коментарі (поки немає)" : $"Коментарі ({comments.Count})";
            DetailsCommentsList.ItemsSource = comments
                .Select(comment => new
                {
                    comment.Id,
                    comment.Content,
                    Header = comment.AuthorRole.HasValue
                        ? $"{comment.AuthorName} ({RoleText(comment.AuthorRole.Value)})"
                        : comment.AuthorName,
                    // У БД час зберігається в UTC — показуємо місцевий
                    Time = comment.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                    DeleteVisibility = comment.CanDelete ? Visibility.Visible : Visibility.Collapsed,
                })
                .ToList();

            if (DetailsCommentsList.Items.Count > 0)
            {
                DetailsCommentsList.ScrollIntoView(DetailsCommentsList.Items[^1]);
            }
        }

        private async void SendComment_Click(object sender, RoutedEventArgs e)
        {
            if (detailsTaskId is not { } taskId)
            {
                return;
            }

            using var busy = UiBusy.Disable(SendCommentButton);
            var result = await commentService.AddAsync(taskId, NewCommentTextBox.Text, CurrentUserId);

            if (result.ShowIfFailed("Коментар не надіслано"))
            {
                return;
            }

            NewCommentTextBox.Clear();
            await LoadCommentsAsync();
        }

        private async void DeleteComment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: Guid commentId })
            {
                return;
            }

            var answer = MessageBox.Show("Видалити коментар?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await commentService.DeleteAsync(commentId, CurrentUserId);
            if (result.IsFailure)
            {
                MessageBox.Show(result.Error!.Message, "Коментар не видалено", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await LoadCommentsAsync();
        }

        // ---------- Підзадачі ----------
        private async Task LoadSubtasksAsync()
        {
            if (detailsTaskId is not { } taskId)
            {
                return;
            }

            var result = await subtaskService.GetByTaskAsync(taskId, CurrentUserId);
            if (result.IsFailure)
            {
                DetailsSubtasksList.ItemsSource = null;
                DetailsSubtasksText.Text = result.Error!.Message;
                DetailsSubtasksProgress.Value = 0;
                return;
            }

            ShowSubtasks(result.Value!);
        }

        private void ShowSubtasks(IReadOnlyList<SubtaskModel> subtasks)
        {
            DetailsSubtasksList.ItemsSource = subtasks;

            var completed = subtasks.Count(subtask => subtask.IsCompleted);
            DetailsSubtasksText.Text = subtasks.Count == 0
                ? "Підзадач немає"
                : $"Виконано {completed} з {subtasks.Count}";
            DetailsSubtasksProgress.Value = subtasks.Count == 0 ? 0 : completed * 100.0 / subtasks.Count;
        }

        private async void AddSubtask_Click(object sender, RoutedEventArgs e)
        {
            await AddSubtaskAsync();
        }

        private async void NewSubtaskTitleTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                await AddSubtaskAsync();
            }
        }

        private async Task AddSubtaskAsync()
        {
            if (detailsTaskId is not { } taskId)
            {
                return;
            }

            var result = await subtaskService.AddAsync(taskId, NewSubtaskTitleTextBox.Text, CurrentUserId);
            if (result.IsFailure)
            {
                MessageBox.Show(result.Error!.Message, "Підзадачу не додано", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewSubtaskTitleTextBox.Clear();
            await LoadSubtasksAsync();
        }

        private async void SubtaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox { Tag: Guid subtaskId } checkBox)
            {
                return;
            }

            var result = await subtaskService.SetCompletedAsync(subtaskId, checkBox.IsChecked == true, CurrentUserId);
            if (result.IsFailure)
            {
                MessageBox.Show(result.Error!.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Перечитуємо з БД: і після успіху (прогрес), і після помилки (повертаємо галочку у справжній стан)
            await LoadSubtasksAsync();
        }

        private async void DeleteSubtask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: Guid subtaskId })
            {
                return;
            }

            var answer = MessageBox.Show("Видалити підзадачу?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await subtaskService.DeleteAsync(subtaskId, CurrentUserId);
            if (result.IsFailure)
            {
                MessageBox.Show(result.Error!.Message, "Підзадачу не видалено", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await LoadSubtasksAsync();
        }

        //..
        private async void SubmitForReview_Click(object sender, RoutedEventArgs e)
        {
            if (detailsTaskId is not { } taskId)
            {
                return;
            }

            using var busy = UiBusy.Disable(DetailsSubmitReviewButton);
            var result = await reviewService.SubmitForReviewAsync(taskId, CurrentUserId);

            if (result.ShowIfFailed("Не вдалося відправити на перевірку"))
            {
                return;
            }

            MessageBox.Show("Завдання відправлено на перевірку!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadTasksAsync();
            var details = await taskService.GetDetailsAsync(taskId, CurrentUserId);
            if (details.IsSuccess)
            {
                ShowTaskDetails(details.Value!);
            }
        }

        private async Task LoadReviewQueueAsync()
        {
            var result = await reviewService.GetReviewQueueAsync(CurrentUserId);
            if (result.ShowIfFailed("Не вдалося завантажити чергу перевірки"))
            {
                return;
            }

            ReviewQueueDataGrid.ItemsSource = result.Value!;
            ReviewSelectedTitleText.Text = result.Value!.Count == 0
                ? "Немає завдань, що очікують перевірки"
                : "Оберіть завдання зі списку ліворуч";
            ReviewSelectedDescriptionText.Text = "Опис вибраного завдання з'явиться тут.";
            ReviewCommentTextBox.Clear();
            ReviewApproveButton.IsEnabled = false;
            ReviewRequestRevisionButton.IsEnabled = false;
        }

        private async void RefreshReviewQueue_Click(object sender, RoutedEventArgs e)
        {
            await LoadReviewQueueAsync();
        }

        private void ReviewQueueDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReviewQueueDataGrid.SelectedItem is ReviewTaskItemModel selected)
            {
                ReviewSelectedTitleText.Text = $"{selected.TaskCode} · {selected.Title}";
                ReviewSelectedDescriptionText.Text = string.IsNullOrWhiteSpace(selected.Description)
                    ? "Немає опису"
                    : selected.Description;
                ReviewApproveButton.IsEnabled = true;
                ReviewRequestRevisionButton.IsEnabled = true;
            }
            else
            {
                ReviewApproveButton.IsEnabled = false;
                ReviewRequestRevisionButton.IsEnabled = false;
            }
        }

        private async void ApproveReview_Click(object sender, RoutedEventArgs e)
        {
            if (ReviewQueueDataGrid.SelectedItem is not ReviewTaskItemModel selected)
            {
                return;
            }

            // Кнопки вимкнені лише на час запиту; після успіху черга оновиться і вимкне їх сама (нічого не вибрано)
            Result result;
            using (UiBusy.Disable(ReviewApproveButton, ReviewRequestRevisionButton))
            {
                result = await reviewService.ApproveAsync(selected.TaskId, CurrentUserId);
            }

            if (result.ShowIfFailed("Не вдалося прийняти завдання"))
            {
                return;
            }

            MessageBox.Show($"Завдання {selected.TaskCode} прийнято (Done)!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadTasksAsync();
            await LoadReviewQueueAsync();
        }

        private async void RequestRevision_Click(object sender, RoutedEventArgs e)
        {
            if (ReviewQueueDataGrid.SelectedItem is not ReviewTaskItemModel selected)
            {
                return;
            }

            Result result;
            using (UiBusy.Disable(ReviewApproveButton, ReviewRequestRevisionButton))
            {
                result = await reviewService.RequestRevisionAsync(selected.TaskId, ReviewCommentTextBox.Text, CurrentUserId);
            }

            if (result.ShowIfFailed("Не вдалося повернути завдання на доопрацювання"))
            {
                return;
            }

            MessageBox.Show($"Завдання {selected.TaskCode} повернуто на доопрацювання!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadTasksAsync();
            await LoadReviewQueueAsync();
        }

        // ---------- Створення завдання: вибір проєкту ----------

        private async Task LoadProjectChoicesAsync()
        {
            var previousId = (ProjectComboBox.SelectedItem as ProjectModel)?.Id;

            var result = await projectService.GetProjectsForNewTaskAsync(CurrentUserId);
            if (result.ShowIfFailed("Не вдалося завантажити проєкти"))
            {
                return;
            }

            var projects = result.Value!;
            ProjectComboBox.ItemsSource = projects;

            // Пам'ятаємо останній вибраний проєкт, щоб не обирати його щоразу заново
            ProjectComboBox.SelectedItem = projects.FirstOrDefault(project => project.Id == previousId) ?? projects.FirstOrDefault();

            var hasProjects = projects.Count > 0;
            ProjectComboBox.IsEnabled = hasProjects;
            CreateTaskButton.IsEnabled = hasProjects;
            NoProjectsHint.Visibility = hasProjects ? Visibility.Collapsed : Visibility.Visible;
        }

        // DatePicker повертає північ обраного дня; дедлайн «на сьогодні» тоді вже в минулому, тому беремо кінець дня
        private static DateTime? EndOfDay(DateTime? date)
        {
            return date?.Date.AddDays(1).AddMinutes(-1);
        }

        private async Task OpenTaskDetailsAsync(Guid taskId)
        {
            var details = await taskService.GetDetailsAsync(taskId, CurrentUserId);
            if (details.ShowIfFailed("Деталі недоступні"))
            {
                return;
            }

            ShowTaskDetails(details.Value!);
            await LoadSubtasksAsync();
            await LoadCommentsAsync();
            ShowPage("Details");
        }

        // ---------- Календар ----------

        private static readonly CultureInfo UkrainianCulture = CultureInfo.GetCultureInfo("uk-UA");

        private DateTime calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

        private DateTime calendarSelectedDay = DateTime.Today;

        private void RenderCalendar()
        {
            var days = TaskCalendar.BuildMonth(allTasks, calendarMonth.Year, calendarMonth.Month, DateTime.Now);
            CalendarDaysControl.ItemsSource = days;
            CalendarMonthTitle.Text = Capitalize(calendarMonth.ToString("MMMM yyyy", UkrainianCulture));

            // Обраний день лишається обраним, якщо він є в сітці; інакше — перше число місяця
            var selected = days.FirstOrDefault(day => day.Date == calendarSelectedDay.Date)
                ?? days.First(day => day.IsCurrentMonth);
            ShowCalendarDay(selected);
        }

        private void ShowCalendarDay(CalendarDayModel day)
        {
            calendarSelectedDay = day.Date;
            CalendarSelectedDayTitle.Text = Capitalize(day.Date.ToString("dddd, d MMMM", UkrainianCulture));

            var now = DateTime.UtcNow;
            CalendarDayTasksList.ItemsSource = day.Tasks
                .Select(task => new CalendarTaskRow(
                    task.Id,
                    task.TaskCode,
                    task.Title,
                    task.Deadline!.Value.ToLocalTime().ToString("HH:mm"),
                    StatusText(task.Status),
                    task.Status != TaskItemStatus.Done && task.Deadline.Value.ToUniversalTime() < now))
                .ToList();
            CalendarNoTasksText.Visibility = day.Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CalendarDay_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not CalendarDayModel day)
            {
                return;
            }

            if (day.IsCurrentMonth)
            {
                ShowCalendarDay(day);
                return;
            }

            // Клік по дню сусіднього місяця переходить на той місяць
            calendarSelectedDay = day.Date;
            calendarMonth = new DateTime(day.Date.Year, day.Date.Month, 1);
            RenderCalendar();
        }

        private void CalendarPrevMonth_Click(object sender, RoutedEventArgs e)
        {
            calendarMonth = calendarMonth.AddMonths(-1);
            RenderCalendar();
        }

        private void CalendarNextMonth_Click(object sender, RoutedEventArgs e)
        {
            calendarMonth = calendarMonth.AddMonths(1);
            RenderCalendar();
        }

        private void CalendarToday_Click(object sender, RoutedEventArgs e)
        {
            calendarSelectedDay = DateTime.Today;
            calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            RenderCalendar();
        }

        private async void CalendarDayTasks_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (CalendarDayTasksList.SelectedItem is CalendarTaskRow row)
            {
                await OpenTaskDetailsAsync(row.Id);
            }
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], UkrainianCulture) + text[1..];
        }

        // Рядок списку завдань обраного дня: усе вже відформатовано для показу
        private sealed record CalendarTaskRow(Guid Id, string Code, string Title, string Time, string Status, bool IsOverdue);
    }
}