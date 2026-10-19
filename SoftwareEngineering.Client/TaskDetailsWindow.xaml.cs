using System;
using System.Windows;
using SoftwareEngineering.BLL.Interfaces;

namespace SoftwareEngineering.Client
{
    public partial class TaskDetailsWindow : Window
    {
        private readonly ITaskService _taskService;
        private readonly Guid _taskId;

        public TaskDetailsWindow(ITaskService taskService, Guid taskId)
        {
            InitializeComponent();
            _taskService = taskService;
            _taskId = taskId;

            Loaded += TaskDetailsWindow_Loaded;
        }

        private async void TaskDetailsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var task = await _taskService.GetByIdAsync(_taskId);

                TitleTextBlock.Text = task.Title;
                StatusTextBlock.Text = $"Статус: {task.Status} | Пріоритет: {task.Priority}";
                DescriptionTextBox.Text = string.IsNullOrWhiteSpace(task.Description) ? "Немає опису" : task.Description;
                DeadlineTextBlock.Text = task.Deadline.HasValue ? $"Дедлайн: {task.Deadline.Value:dd.MM.yyyy HH:mm}" : "Дедлайн: Не вказано";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}