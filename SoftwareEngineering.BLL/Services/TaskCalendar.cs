using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Розкладає завдання по днях місяця за дедлайном. Чиста функція над уже завантаженим списком
/// (як TaskFilter): у БД не ходить, тож Result і логування тут не потрібні.
/// </summary>
public static class TaskCalendar
{
    public const int DaysInGrid = 42; // 6 тижнів — вміщується будь-який місяць

    public const int DefaultPreviewSize = 3;

    /// <summary>
    /// Сітка місяця з понеділка: дні попереднього місяця, увесь місяць, дні наступного — разом 42 клітинки.
    /// Дедлайн порівнюється за локальною датою, як і у фільтрі за періодом.
    /// selectedDate позначає обраний день (IsSelected), щоб вікно могло його підсвітити.
    /// </summary>
    public static IReadOnlyList<CalendarDayModel> BuildMonth(
        IEnumerable<TaskModel> tasks,
        int year,
        int month,
        DateTime now,
        DateTime? selectedDate = null,
        int previewSize = DefaultPreviewSize)
    {
        var firstDay = new DateTime(year, month, 1);
        var gridStart = firstDay.AddDays(-DaysFromMonday(firstDay.DayOfWeek));
        var gridEnd = gridStart.AddDays(DaysInGrid);
        var today = now.Date;
        var nowUtc = now.ToUniversalTime();

        var tasksByDay = tasks
            .Where(task => task.Deadline.HasValue)
            .Select(task => (Task: task, Day: task.Deadline!.Value.ToLocalTime().Date))
            .Where(item => item.Day >= gridStart && item.Day < gridEnd)
            .GroupBy(item => item.Day)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TaskModel>)group
                    .Select(item => item.Task)
                    .OrderBy(task => task.Deadline)
                    .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ToList());

        var days = new List<CalendarDayModel>(DaysInGrid);
        for (var offset = 0; offset < DaysInGrid; offset++)
        {
            var date = gridStart.AddDays(offset);
            var dayTasks = tasksByDay.TryGetValue(date, out var found) ? found : Array.Empty<TaskModel>();

            days.Add(new CalendarDayModel
            {
                Date = date,
                IsCurrentMonth = date.Month == month,
                IsToday = date == today,
                IsSelected = selectedDate.HasValue && date == selectedDate.Value.Date,
                Tasks = dayTasks,
                PreviewTasks = dayTasks.Take(Math.Max(previewSize, 0)).ToList(),
                HasOverdue = dayTasks.Any(task => IsOverdue(task, nowUtc)),
            });
        }

        return days;
    }

    private static bool IsOverdue(TaskModel task, DateTime nowUtc)
    {
        return task.Status != TaskItemStatus.Done
            && task.Deadline.HasValue
            && task.Deadline.Value.ToUniversalTime() < nowUtc;
    }

    // DayOfWeek рахує від неділі, а календар в Україні починається з понеділка
    private static int DaysFromMonday(DayOfWeek dayOfWeek)
    {
        return ((int)dayOfWeek + 6) % 7;
    }
}
