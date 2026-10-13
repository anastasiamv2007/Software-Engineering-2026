using SoftwareEngineering.BLL.Exceptions;
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

        public CreateTaskView(ITaskService taskService)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));

            DeadlineDatePicker.SelectedDate = DateTime.Now.AddDays(1);
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var priority = TaskPriority.Medium;
                if (PriorityComboBox.SelectedItem is ComboBoxItem selectedItem &&
                    selectedItem.Tag is TaskPriority parsedPriority)
                {
                    priority = parsedPriority;
                }

                var taskModel = new TaskModel
                {
                    Title = TitleTextBox.Text?.Trim() ?? string.Empty,
                    Description = DescriptionTextBox.Text?.Trim() ?? string.Empty,
                    Deadline = DeadlineDatePicker.SelectedDate ?? DateTime.Now,
                    Priority = priority,
                    Status = TaskItemStatus.Todo
                };

                await taskService.CreateAsync(taskModel);

                MessageBox.Show("Завдання успішно створено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

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