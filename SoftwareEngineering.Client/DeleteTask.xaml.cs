using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SoftwareEngineering.Client
{
    public partial class DeleteTaskWindow : Window
    {
        private readonly ITaskService _taskService;

        public DeleteTaskWindow(ITaskService taskService)
        {
            InitializeComponent();
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }


        // Use Case 4

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(186, 194, 222));
            StatusTextBlock.Text = "Виконується запит (UC 4)...";

            if (!Guid.TryParse(TaskIdTextBox.Text.Trim(), out Guid taskId))
            {
                SetError("Помилка вводу: Task ID має бути валідним Guid (наприклад: 3fa85f64-5717-4562-b3fc-2c963f66afa6)");
                return;
            }

            if (!Guid.TryParse(UserIdTextBox.Text.Trim(), out Guid userId))
            {
                SetError("Помилка вводу: User ID має бути валідним Guid.");
                return;
            }

            var result = await _taskService.DeleteTaskByOwnerAsync(new TaskModel { Id = taskId }, userId);
            if (result.IsFailure)
            {
                SetError($"[ВІДХИЛЕНО]: {result.Error!.Message}");
                return;
            }

            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(166, 227, 161)); // Зелений
            StatusTextBlock.Text = $"[УСПІХ UC 4]: Завдання {taskId} успішно видалено користувачем {userId}!";
        }

        // Use Case 5
      
        private async void DeleteSharedTaskButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(186, 194, 222));
            StatusTextBlock.Text = "Виконується запит (Use Case 5)...";

            if (!Guid.TryParse(TaskIdTextBox.Text.Trim(), out Guid taskId))
            {
                SetError("Помилка вводу: Task ID має бути валідним Guid.");
                return;
            }

            if (!Guid.TryParse(UserIdTextBox.Text.Trim(), out Guid userId))
            {
                SetError("Помилка вводу: User ID має бути валідним Guid.");
                return;
            }

            var result = await _taskService.DeleteTaskAsync(taskId, userId);
            if (result.IsFailure)
            {
                SetError($"[ВІДМОВА (UC 5)]: {result.Error!.Message}");
                return;
            }

            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(166, 227, 161));
            StatusTextBlock.Text = $"[УСПІХ UC 5]: Спільне завдання {taskId} успішно видалено власником {userId}!";
        }

        private void SetError(string message)
        {
            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(243, 139, 168));
            StatusTextBlock.Text = message;
        }
    }
}