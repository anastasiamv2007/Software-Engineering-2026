using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.Tests;

public class TaskCalendarTests
{
    // 24 листопада 2026 — вівторок; 1 листопада 2026 — неділя
    private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void BuildMonth_StartsOnMondayAndHasSixWeeks()
    {
        // Act
        var days = TaskCalendar.BuildMonth(Array.Empty<TaskModel>(), 2026, 11, Now);

        // Assert
        Assert.Equal(42, days.Count);
        Assert.Equal(DayOfWeek.Monday, days[0].Date.DayOfWeek);
        Assert.Equal(new DateTime(2026, 10, 26), days[0].Date);
        Assert.False(days[0].IsCurrentMonth);
        Assert.Equal(30, days.Count(day => day.IsCurrentMonth));
    }

    [Fact]
    public void BuildMonth_MarksToday()
    {
        // Act
        var days = TaskCalendar.BuildMonth(Array.Empty<TaskModel>(), 2026, 11, Now);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 24), Assert.Single(days, day => day.IsToday).Date);
    }

    [Fact]
    public void BuildMonth_PutsTaskOnLocalDeadlineDate_SortedByTime()
    {
        // Arrange
        var evening = Task("Вечір", new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Local));
        var morning = Task("Ранок", new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Local));

        // Act
        var days = TaskCalendar.BuildMonth(new[] { evening, morning }, 2026, 11, Now);

        // Assert
        var day = days.Single(d => d.Date == new DateTime(2026, 11, 20));
        Assert.Equal(new[] { "Ранок", "Вечір" }, day.Tasks.Select(task => task.Title));
        Assert.All(days.Where(d => d.Date != day.Date), d => Assert.Empty(d.Tasks));
    }

    // Завдання з дедлайном у сусідньому місяці видно в доповнених клітинках сітки
    [Fact]
    public void BuildMonth_IncludesTasksOfNeighbourDaysInGrid()
    {
        // Arrange
        var task = Task("Жовтень", new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Local));

        // Act
        var days = TaskCalendar.BuildMonth(new[] { task }, 2026, 11, Now);

        // Assert
        var day = days.Single(d => d.Date == new DateTime(2026, 10, 27));
        Assert.False(day.IsCurrentMonth);
        Assert.Single(day.Tasks);
    }

    [Fact]
    public void BuildMonth_TasksWithoutDeadlineOrOutsideGrid_AreIgnored()
    {
        // Arrange
        var noDeadline = Task("Без дедлайну", null);
        var farAway = Task("Наступного року", new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Local));

        // Act
        var days = TaskCalendar.BuildMonth(new[] { noDeadline, farAway }, 2026, 11, Now);

        // Assert
        Assert.All(days, day => Assert.Empty(day.Tasks));
    }

    [Fact]
    public void BuildMonth_LimitsPreviewAndCountsHidden()
    {
        // Arrange
        var date = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Local);
        var tasks = Enumerable.Range(1, 5).Select(i => Task($"Завдання {i}", date.AddMinutes(i))).ToList();

        // Act
        var day = TaskCalendar.BuildMonth(tasks, 2026, 11, Now, previewSize: 3).Single(d => d.Date == date.Date);

        // Assert
        Assert.Equal(3, day.PreviewTasks.Count);
        Assert.Equal(2, day.HiddenTasksCount);
    }

    // Прострочене — лише невиконане з дедлайном у минулому
    [Fact]
    public void BuildMonth_MarksOverdueOnlyForUnfinishedPastTasks()
    {
        // Arrange
        var past = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Local);
        var overdue = Task("Прострочене", past);
        var doneLate = Task("Виконане", past.AddDays(1), TaskItemStatus.Done);
        var future = Task("Майбутнє", Now.AddDays(1));

        // Act
        var days = TaskCalendar.BuildMonth(new[] { overdue, doneLate, future }, 2026, 11, Now);

        // Assert
        Assert.True(days.Single(d => d.Date == past.Date).HasOverdue);
        Assert.False(days.Single(d => d.Date == past.AddDays(1).Date).HasOverdue);
        Assert.False(days.Single(d => d.Date == Now.AddDays(1).Date).HasOverdue);
    }

    private static TaskModel Task(string title, DateTime? deadline, TaskItemStatus status = TaskItemStatus.Todo)
    {
        return new TaskModel { Id = Guid.NewGuid(), Title = title, Deadline = deadline, Status = status };
    }
}
