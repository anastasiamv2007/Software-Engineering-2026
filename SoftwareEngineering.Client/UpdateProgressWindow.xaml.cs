using SoftwareEngineering.BLL.Exceptions;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.DAL.Enums;
using System.Windows;
using System.Windows.Controls;

namespace SoftwareEngineering.Client
{
    public partial class UpdateProgressWindow : Window
    {
        private readonly ITaskService taskService;

        public UpdateProgressWindow(ITaskService taskService)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ = Guid.TryParse(TaskIdTextBox.Text?.Trim(), out var taskId);
                _ = Guid.TryParse(UserIdTextBox.Text?.Trim(), out var userId);

                var status = TaskItemStatus.InProgress;
                if (StatusComboBox.SelectedItem is ComboBoxItem selectedItem &&
                    selectedItem.Tag is TaskItemStatus parsedStatus)
                {
                    status = parsedStatus;
                }

                await taskService.UpdateProgressAsync(taskId, status, userId);

                MessageBox.Show("Прогрес успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}