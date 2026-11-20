using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Enums;
using System.Windows;
using System.Windows.Controls;

namespace SoftwareEngineering.Client
{
    public partial class CreateTaskView : Window
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;
        private readonly IUserSession userSession;

        public CreateTaskView(ITaskService taskService, IProjectService projectService, IUserSession userSession)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));

            DeadlineDatePicker.SelectedDate = DateTime.Now.AddDays(1);
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            var priority = TaskPriority.Medium;
            if (PriorityComboBox.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is TaskPriority parsedPriority)
            {
                priority = parsedPriority;
            }

            var ownerId = userSession.CurrentUserId ?? Guid.Empty;
            var project = await projectService.GetOrCreateByNameAsync(ProjectTextBox.Text ?? string.Empty, ownerId);
            if (project.ShowIfFailed("Завдання не створено"))
            {
                return;
            }

            var created = await taskService.CreateAsync(new TaskModel
            {
                ProjectId = project.Value!.Id,
                OwnerId = ownerId,
                Title = TitleTextBox.Text?.Trim() ?? string.Empty,
                Description = DescriptionTextBox.Text?.Trim() ?? string.Empty,
                Deadline = DeadlineDatePicker.SelectedDate ?? DateTime.Now,
                Priority = priority,
                Status = TaskItemStatus.Todo
            });
            if (created.ShowIfFailed("Завдання не створено"))
            {
                return;
            }

            MessageBox.Show("Завдання успішно створено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}