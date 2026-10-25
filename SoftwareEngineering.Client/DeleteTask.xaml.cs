using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.BLL.Exceptions;
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

            try
            {
                var model = new TaskModel { Id = taskId };
                await _taskService.DeleteTaskByOwnerAsync(model, userId);

                StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(166, 227, 161)); // Зелений
                StatusTextBlock.Text = $"[УСПІХ UC 4]: Завдання {taskId} успішно видалено користувачем {userId}!";
            }
            catch (ValidationException valEx)
            {
                SetError($"[ВІДХИЛЕНО BLL ВАЛІДАЦІЄЮ]: {valEx.Message}");
            }
            catch (Exception ex)
            {
                SetError($"[СИСТЕМНА ПОМИЛКА]: {ex.Message}");
            }
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

            try
            {
                await _taskService.DeleteTaskAsync(taskId, userId);

                StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(166, 227, 161));
                StatusTextBlock.Text = $"[УСПІХ UC 5]: Спільне завдання {taskId} успішно видалено власником {userId}!";
            }
            catch (Exception ex)
            {
                SetError($"[ВІДМОВА ДОСТУПУ (UC 5)]: {ex.Message}");
            }
        }

        private void SetError(string message)
        {
            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(243, 139, 168));
            StatusTextBlock.Text = message;
        }
    }
}