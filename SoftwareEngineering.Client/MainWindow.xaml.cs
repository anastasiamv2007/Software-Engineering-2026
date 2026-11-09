using Serilog;
using SoftwareEngineering.BLL.Exceptions;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Enums;
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
        private CategoryModel? selectedCategory;
        private string selectedCategoryColor = CategoryPalette[0];


        private static readonly string[] CategoryPalette =
        {
            "#3B82F6", "#EC4899", "#8B5CF6", "#10B981", "#F59E0B", "#EF4444", "#06B6D4", "#6366F1",
        };

        public MainWindow(ITaskService taskService, IProjectService projectService, IUserSession userSession, ICategoryService categoryService, IProjectMemberService projectMemberService)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
            this.categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));

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
            var result = MessageBox.Show("Ви дійсно хочете вийти з облікового запису?", "Вихід", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ((App)Application.Current).SignOut();
            }
        }

        private void SignOutAndDisconnect_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вийти з облікового запису та обрати іншу базу даних?", "Змінити базу", MessageBoxButton.YesNo, MessageBoxImage.Question);
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

        private void ShowPage(string page)
        {
            foreach (FrameworkElement element in PagesHost.Children)
            {
                element.Visibility = element.Name == $"Page_{page}" ? Visibility.Visible : Visibility.Collapsed;
            }

            // Список категорій оновлюємо щоразу, коли відкривається вкладка
            if (page == "Categories")
            {
                _ = LoadCategoriesAsync();
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
                // Лише завдання проєктів, де користувач — власник або співвиконавець
                allTasks = await taskService.GetUserTasksAsync(CurrentUserId);
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
            if (allTasks == null) return;

            var criteria = new TaskFilterCriteria
            {
                Status = GetSelectedEnum<TaskItemStatus>(FilterStatusComboBox),
                Priority = GetSelectedEnum<TaskPriority>(FilterPriorityComboBox),
                CategoryId = GetSelectedCategoryId(FilterCategoryComboBox),
                OnlyOverdue = FilterOverdueCheckBox.IsChecked ?? false,
                SortBy = GetSelectedEnum<TaskSortOption>(SortComboBox) ?? TaskSortOption.ByDeadlineAsc,
            };

            var filterResult = TaskFilter.Apply(allTasks, criteria);
            if (filterResult.IsFailure)
            {
                Log.Warning("Помилка фільтрації задач: {Error}", filterResult.Error!.Message);
                return;
            }

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
            SearchBox.Text = string.Empty;

            ApplyFilter();
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
                    CategoryId = GetSelectedCategoryId(CategoryComboBox),
                    Status = TaskItemStatus.Todo,
                });

                MessageBox.Show("Завдання успішно створено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                TitleTextBox.Clear();
                DescriptionTextBox.Clear();
                CategoryComboBox.SelectedIndex = 0;
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
                    ShowTaskDetails(await taskService.GetDetailsAsync(selected.Id, CurrentUserId));
                    ShowPage("Details");
                }
                catch (ValidationException ex)
                {
                    MessageBox.Show(ex.Message, "Немає доступу", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            DetailsSubtasksText.Text = details.SubtasksTotal == 0
                ? "Підзадач немає"
                : $"Підзадачі: виконано {details.SubtasksCompleted} з {details.SubtasksTotal}";
            DetailsSubtasksProgress.Value = details.SubtasksTotal == 0 ? 0 : details.SubtasksCompleted * 100.0 / details.SubtasksTotal;
            DetailsCommentsText.Text = $"Коментарів: {details.CommentsCount}";

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
            ProjectRole.Reviewer => "Рецензент",
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
                currentSelectedTask.CategoryId = GetSelectedCategoryId(EditCategoryComboBox);

                await taskService.UpdateAsync(currentSelectedTask, CurrentUserId);
                if (EditAssigneeComboBox.IsEnabled)
                {
                    var newAssigneeId = (EditAssigneeComboBox.SelectedItem as ComboBoxItem)?.Tag as Guid?;
                    if (newAssigneeId != currentSelectedTask.AssigneeId)
                    {
                        var assignResult = await taskService.AssignAsync(currentSelectedTask.Id, newAssigneeId, CurrentUserId);
                        if (assignResult.IsFailure)
                        {
                            MessageBox.Show(assignResult.Error!.Message, "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

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


    }
}