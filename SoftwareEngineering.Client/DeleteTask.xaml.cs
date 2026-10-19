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

namespace SoftwareEngineering.Client {
    public partial class DeleteTaskWindow : Window {
        private readonly ITaskService _taskService;

        // Отримує сервіс
        public DeleteTaskWindow(ITaskService taskService) {
            InitializeComponent();
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e) {
            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(186, 194, 222));
            StatusTextBlock.Text = "Виконується запит...";

            // 1. Зчитуємо TaskId
            if (!Guid.TryParse(TaskIdTextBox.Text.Trim(), out Guid taskId)) {
                SetError("Помилка вводу: Task ID має бути валідним Guid (наприклад: 3fa85f64-5717-4562-b3fc-2c963f66afa6)");
                return;
            }

            // 2. Зчитуємо UserId
            if (!Guid.TryParse(UserIdTextBox.Text.Trim(), out Guid userId)) {
                SetError("Помилка вводу: User ID має бути валідним Guid.");
                return;
            }

            try {
                // Створюємо мінімальну модель з цим ID
                var model = new TaskModel { Id = taskId };

                // 3. Викликаємо твій сервіс
                await _taskService.DeleteTaskByOwnerAsync(model, userId);

                // Якщо дійшло сюди і не впало — успіх!
                StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(166, 227, 161)); // Зелений
                StatusTextBlock.Text = $"[УСПІХ]: Завдання {taskId} успішно видалено користувачем {userId}!";
            }
            catch (ValidationException valEx) {
                // Якщо спрацювала бізнес-перевірка (наприклад, юзер не Owner)
                SetError($"[ВІДХИЛЕНО BLL ВАЛІДАЦІЄЮ]: {valEx.Message}");
            }
            catch (Exception ex) {
                // Інші помилки (наприклад, впала база)
                SetError($"[СИСТЕМНА ПОМИЛКА]: {ex.Message}");
            }
        }

        private void SetError(string message) {
            StatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(243, 139, 168)); // Червоний
            StatusTextBlock.Text = message;
        }
    }
}
