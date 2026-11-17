using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Enums;
using System;
using System.Windows;
using System.Windows.Controls;
using Serilog; 

namespace SoftwareEngineering.Client
{
    public partial class EditTaskWindow : Window
    {
        private readonly ITaskService taskService;
        private readonly TaskModel currentTask;
        private readonly Guid currentUserId;

        public EditTaskWindow(ITaskService taskService, TaskModel taskToEdit, Guid currentUserId)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.currentTask = taskToEdit ?? throw new ArgumentNullException(nameof(taskToEdit));
            this.currentUserId = currentUserId;

            TitleTextBox.Text = currentTask.Title;
            DescriptionTextBox.Text = currentTask.Description;
            DeadlineDatePicker.SelectedDate = currentTask.Deadline;

            foreach (ComboBoxItem item in PriorityComboBox.Items)
            {
                if (item.Tag is TaskPriority priority && priority == currentTask.Priority)
                {
                    PriorityComboBox.SelectedItem = item;
                    break;
                }
            }
            // ПЕРЕВІРКА ПРАВ ДЛЯ USE-CASE #6
            if (currentTask.OwnerId != currentUserId)
            {
                DeadlineDatePicker.IsEnabled = false;
                DeadlineDatePicker.ToolTip = "Лише власник завдання може змінювати дедлайн.";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var priority = TaskPriority.Medium;
            if (PriorityComboBox.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is TaskPriority parsedPriority)
            {
                priority = parsedPriority;
            }

            currentTask.Title = TitleTextBox.Text?.Trim() ?? string.Empty;
            currentTask.Description = DescriptionTextBox.Text?.Trim() ?? string.Empty;
            var picked = DeadlineDatePicker.SelectedDate;
            if (!(currentTask.Deadline.HasValue && picked.HasValue && currentTask.Deadline.Value.ToLocalTime().Date == picked.Value.Date))
            {
                currentTask.Deadline = picked;
            }
            currentTask.Priority = priority;

            // Успіх і відмову логує сервіс (BaseService.ExecuteAsync), тут лише показ
            var result = await taskService.UpdateAsync(currentTask, currentUserId);
            if (result.ShowIfFailed("Завдання не оновлено"))
            {
                return;
            }

            MessageBox.Show("Завдання успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

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