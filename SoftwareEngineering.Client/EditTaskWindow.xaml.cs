using SoftwareEngineering.BLL.Exceptions;
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

        public EditTaskWindow(ITaskService taskService, TaskModel taskToEdit)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.currentTask = taskToEdit ?? throw new ArgumentNullException(nameof(taskToEdit));

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
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var priority = TaskPriority.Medium;
                if (PriorityComboBox.SelectedItem is ComboBoxItem selectedItem &&
                    selectedItem.Tag is TaskPriority parsedPriority)
                {
                    priority = parsedPriority;
                }

                currentTask.Title = TitleTextBox.Text?.Trim() ?? string.Empty;
                currentTask.Description = DescriptionTextBox.Text?.Trim() ?? string.Empty;
                currentTask.Deadline = DeadlineDatePicker.SelectedDate;
                currentTask.Priority = priority;

                await taskService.UpdateAsync(currentTask);

                //3
                Log.Information("Користувач успішно оновив завдання. ID: {TaskId}, Назва: {TaskTitle}", currentTask.Id, currentTask.Title);

                MessageBox.Show("Завдання успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (ValidationException ex)
            {
                Log.Warning("Помилка валідації при оновленні завдання {TaskId}: {Message}", currentTask.Id, ex.Message);
                MessageBox.Show(ex.Message, "Помилка валідації", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Критична помилка при оновленні завдання {TaskId}", currentTask.Id);
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