using System.Windows;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.Client;

/// <summary>
/// Діалог копіювання: користувач змінює лише назву, дедлайн і чи копіювати підзадачі.
/// </summary>
public partial class DuplicateTaskWindow : Window
{
    private const string CopyPrefix = "Копія: ";

    private readonly ITaskService taskService;
    private readonly TaskModel source;
    private readonly Guid currentUserId;

    public DuplicateTaskWindow(ITaskService taskService, TaskModel source, Guid currentUserId)
    {
        InitializeComponent();
        this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.currentUserId = currentUserId;

        SourceText.Text = $"Оригінал: {source.TaskCode} «{source.Title}»";
        TitleTextBox.Text = CopyPrefix + source.Title;

        // Старий дедлайн оригіналу часто вже минув — тоді пропонуємо завтрашній день
        var tomorrow = DateTime.Today.AddDays(1);
        var sourceDeadline = source.Deadline?.ToLocalTime().Date;
        DeadlinePicker.SelectedDate = sourceDeadline > DateTime.Today ? sourceDeadline : tomorrow;
        DeadlinePicker.DisplayDateStart = tomorrow;

        Loaded += (_, _) =>
        {
            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        };
    }

    /// <summary>
    /// Створена копія — щоб головне вікно показало її код у повідомленні.
    /// </summary>
    public TaskModel? CreatedTask { get; private set; }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        CreateButton.IsEnabled = false;
        try
        {
            var result = await taskService.DuplicateAsync(
                source.Id,
                new DuplicateTaskModel
                {
                    Title = TitleTextBox.Text,
                    Deadline = DeadlinePicker.SelectedDate,
                    CopySubtasks = CopySubtasksCheckBox.IsChecked == true,
                },
                currentUserId);

            if (result.IsFailure)
            {
                MessageBox.Show(result.Error!.Message, "Копію не створено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreatedTask = result.Value;
            DialogResult = true;
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
