using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.Tests
{
    public class TaskFilterTests
    {
        // 1. Тестуємо патерн Result: чи не падає програма при null
        [Fact]
        public void Apply_TasksListIsNull_ReturnsFailureResultWithoutThrowing()
        {
            var criteria = new TaskFilterCriteria();

            var result = TaskFilter.Apply(null!, criteria);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Validation, result.Error!.Type);
        }

        // 2. Тестуємо найскладнішу логіку: Тільки прострочені (Overdue)
        [Fact]
        public void Apply_OnlyOverdueCriteria_ReturnsOnlyOverdueAndNotDoneTasks()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Id = Guid.NewGuid(), Title = "Прострочена і в роботі", Status = TaskItemStatus.InProgress, Deadline = DateTime.UtcNow.AddDays(-1) },
                new() { Id = Guid.NewGuid(), Title = "Прострочена але Виконана", Status = TaskItemStatus.Done, Deadline = DateTime.UtcNow.AddDays(-1) },
                new() { Id = Guid.NewGuid(), Title = "Майбутня задача", Status = TaskItemStatus.Todo, Deadline = DateTime.UtcNow.AddDays(1) },
                new() { Id = Guid.NewGuid(), Title = "Без дедлайну", Status = TaskItemStatus.Todo, Deadline = null }
            };
            var criteria = new TaskFilterCriteria { OnlyOverdue = true };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.True(result.IsSuccess);
            var filteredTasks = result.Value!.ToList();

            Assert.Single(filteredTasks); // Має залишитися тільки ОДНА задача
            Assert.Equal("Прострочена і в роботі", filteredTasks.First().Title);
        }

        // 3. Тестуємо сортування дедлайнів: задачі без дедлайну (null) мають бути в кінці!
        [Fact]
        public void Apply_SortByDeadline_PutsNullDeadlinesAtTheEnd()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Id = Guid.NewGuid(), Title = "Без дедлайну 1", Deadline = null },
                new() { Id = Guid.NewGuid(), Title = "Завтра", Deadline = DateTime.UtcNow.AddDays(1) },
                new() { Id = Guid.NewGuid(), Title = "Без дедлайну 2", Deadline = null },
                new() { Id = Guid.NewGuid(), Title = "Сьогодні", Deadline = DateTime.UtcNow.AddHours(2) }
            };
            var criteria = new TaskFilterCriteria { SortBy = TaskSortOption.ByDeadlineAsc };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.True(result.IsSuccess);
            var sortedTasks = result.Value!.ToList();

            // Перевіряємо порядок
            Assert.Equal("Сьогодні", sortedTasks[0].Title);
            Assert.Equal("Завтра", sortedTasks[1].Title);
            Assert.Null(sortedTasks[2].Deadline);
            Assert.Null(sortedTasks[3].Deadline);
        }

        // 4. Тестуємо стандартні фільтри (Статус та Пріоритет)
        [Fact]
        public void Apply_StatusAndPriorityCriteria_FiltersCorrectly()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "1", Status = TaskItemStatus.Todo, Priority = TaskPriority.High },
                new() { Title = "2", Status = TaskItemStatus.InProgress, Priority = TaskPriority.High },
                new() { Title = "3", Status = TaskItemStatus.InProgress, Priority = TaskPriority.Low }
            };

            var criteria = new TaskFilterCriteria
            {
                Status = TaskItemStatus.InProgress,
                Priority = TaskPriority.High
            };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.True(result.IsSuccess);
            var filteredTasks = result.Value!.ToList();

            Assert.Single(filteredTasks);
            Assert.Equal("2", filteredTasks.First().Title);
        }
    
        // 5. Сортування за пріоритетом: High → Medium → Low (у enum High = 0, тому це OrderBy)
        [Fact]
        public void Apply_SortByPriority_ReturnsHighFirst()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "low", Priority = TaskPriority.Low },
                new() { Title = "high", Priority = TaskPriority.High },
                new() { Title = "medium", Priority = TaskPriority.Medium },
            };
            var criteria = new TaskFilterCriteria { SortBy = TaskSortOption.ByPriorityDesc };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { "high", "medium", "low" }, result.Value!.Select(t => t.Title));
        }

        // 6. Фільтр за категорією
        [Fact]
        public void Apply_CategoryCriteria_ReturnsOnlyTasksOfThatCategory()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "навчання", CategoryId = 1 },
                new() { Title = "робота", CategoryId = 2 },
                new() { Title = "без категорії", CategoryId = null },
            };
            var criteria = new TaskFilterCriteria { CategoryId = 1 };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("навчання", Assert.Single(result.Value!).Title);
        }

        // 7. Сортування за назвою без урахування регістру
        [Fact]
        public void Apply_SortByTitle_IsAlphabeticalIgnoringCase()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "в" },
                new() { Title = "А" },
                new() { Title = "б" },
            };
            var criteria = new TaskFilterCriteria { SortBy = TaskSortOption.ByTitleAsc };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal(new[] { "А", "б", "в" }, result.Value!.Select(t => t.Title));
        }

        // 8. Порожній список і null-критерії
        [Fact]
        public void Apply_EmptyList_ReturnsEmptySuccess()
        {
            var result = TaskFilter.Apply(new List<TaskModel>(), new TaskFilterCriteria());

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public void Apply_CriteriaIsNull_ReturnsValidationError()
        {
            var result = TaskFilter.Apply(new List<TaskModel>(), null!);

            Assert.Equal(ErrorType.Validation, result.Error!.Type);
        }

        // №5. Період за дедлайном: обидві межі включно, задачі без дедлайну не потрапляють
        [Fact]
        public void Apply_DeadlineRange_ReturnsTasksInsideRangeInclusive()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "до періоду", Deadline = LocalDate(2026, 11, 9) },
                new() { Title = "перший день", Deadline = LocalDate(2026, 11, 10) },
                new() { Title = "середина", Deadline = LocalDate(2026, 11, 15) },
                new() { Title = "останній день", Deadline = LocalDate(2026, 11, 20, 23, 30) },
                new() { Title = "після періоду", Deadline = LocalDate(2026, 11, 21) },
                new() { Title = "без дедлайну", Deadline = null },
            };
            var criteria = new TaskFilterCriteria
            {
                DeadlineFrom = new DateTime(2026, 11, 10),
                DeadlineTo = new DateTime(2026, 11, 20),
                SortBy = TaskSortOption.ByDeadlineAsc,
            };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal(new[] { "перший день", "середина", "останній день" }, result.Value!.Select(t => t.Title));
        }

        // Можна задати лише одну межу: «з» — усе від дати і далі
        [Fact]
        public void Apply_OnlyDeadlineFrom_ReturnsTasksFromThatDate()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "раніше", Deadline = LocalDate(2026, 11, 1) },
                new() { Title = "пізніше", Deadline = LocalDate(2026, 12, 1) },
            };
            var criteria = new TaskFilterCriteria { DeadlineFrom = new DateTime(2026, 11, 15) };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal("пізніше", Assert.Single(result.Value!).Title);
        }

        // «по» — усе до дати включно
        [Fact]
        public void Apply_OnlyDeadlineTo_ReturnsTasksUntilThatDate()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "раніше", Deadline = LocalDate(2026, 11, 1) },
                new() { Title = "пізніше", Deadline = LocalDate(2026, 12, 1) },
            };
            var criteria = new TaskFilterCriteria { DeadlineTo = new DateTime(2026, 11, 15) };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal("раніше", Assert.Single(result.Value!).Title);
        }

        // Межі переплутано — Result з помилкою валідації замість порожнього списку чи винятку
        [Fact]
        public void Apply_DeadlineFromAfterTo_ReturnsValidationError()
        {
            // Arrange
            var criteria = new TaskFilterCriteria
            {
                DeadlineFrom = new DateTime(2026, 11, 20),
                DeadlineTo = new DateTime(2026, 11, 10),
            };

            // Act
            var result = TaskFilter.Apply(new List<TaskModel>(), criteria);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Validation, result.Error!.Type);
        }

        // Один день у «з» і «по» — це фільтр на конкретну дату
        [Fact]
        public void Apply_SameDayRange_ReturnsTasksOfThatDayOnly()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "вранці", Deadline = LocalDate(2026, 11, 18, 8, 0) },
                new() { Title = "ввечері", Deadline = LocalDate(2026, 11, 18, 22, 0) },
                new() { Title = "наступного дня", Deadline = LocalDate(2026, 11, 19, 0, 30) },
            };
            var day = new DateTime(2026, 11, 18);
            var criteria = new TaskFilterCriteria { DeadlineFrom = day, DeadlineTo = day };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal(new[] { "вранці", "ввечері" }, result.Value!.Select(t => t.Title));
        }

        // №9. Сортування за статусом: групи за ходом роботи, а не за числом у enum (Done = 3, NeedsRevision = 4)
        [Fact]
        public void Apply_SortByStatus_GroupsInWorkflowOrder()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "виконано", Status = TaskItemStatus.Done },
                new() { Title = "на доопрацюванні", Status = TaskItemStatus.NeedsRevision },
                new() { Title = "до виконання", Status = TaskItemStatus.Todo },
                new() { Title = "на перевірці", Status = TaskItemStatus.InReview },
                new() { Title = "в роботі", Status = TaskItemStatus.InProgress },
            };
            var criteria = new TaskFilterCriteria { SortBy = TaskSortOption.ByStatus };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal(
                new[] { "до виконання", "в роботі", "на доопрацюванні", "на перевірці", "виконано" },
                result.Value!.Select(t => t.Title));
        }

        // Усередині групи — найближчий дедлайн першим, без дедлайну — в кінці групи
        [Fact]
        public void Apply_SortByStatus_InsideGroupOrdersByDeadlineWithNullsLast()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "todo без дедлайну", Status = TaskItemStatus.Todo, Deadline = null },
                new() { Title = "todo пізня", Status = TaskItemStatus.Todo, Deadline = LocalDate(2026, 12, 1) },
                new() { Title = "todo рання", Status = TaskItemStatus.Todo, Deadline = LocalDate(2026, 11, 20) },
                new() { Title = "в роботі", Status = TaskItemStatus.InProgress, Deadline = LocalDate(2026, 11, 1) },
            };
            var criteria = new TaskFilterCriteria { SortBy = TaskSortOption.ByStatus };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal(
                new[] { "todo рання", "todo пізня", "todo без дедлайну", "в роботі" },
                result.Value!.Select(t => t.Title));
        }

        // Період і сортування за статусом працюють разом з іншими фільтрами
        [Fact]
        public void Apply_DeadlineRangeWithStatusSortAndPriority_CombinesFilters()
        {
            // Arrange
            var tasks = new List<TaskModel>
            {
                new() { Title = "підходить, виконано", Status = TaskItemStatus.Done, Priority = TaskPriority.High, Deadline = LocalDate(2026, 11, 12) },
                new() { Title = "підходить, в роботі", Status = TaskItemStatus.InProgress, Priority = TaskPriority.High, Deadline = LocalDate(2026, 11, 14) },
                new() { Title = "не той пріоритет", Status = TaskItemStatus.Todo, Priority = TaskPriority.Low, Deadline = LocalDate(2026, 11, 13) },
                new() { Title = "поза періодом", Status = TaskItemStatus.Todo, Priority = TaskPriority.High, Deadline = LocalDate(2026, 12, 13) },
            };
            var criteria = new TaskFilterCriteria
            {
                Priority = TaskPriority.High,
                DeadlineFrom = new DateTime(2026, 11, 10),
                DeadlineTo = new DateTime(2026, 11, 20),
                SortBy = TaskSortOption.ByStatus,
            };

            // Act
            var result = TaskFilter.Apply(tasks, criteria);

            // Assert
            Assert.Equal(new[] { "підходить, в роботі", "підходить, виконано" }, result.Value!.Select(t => t.Title));
        }

        // Дедлайн у БД зберігається в UTC; у тестах задаємо місцевий час і переводимо, як це робить Npgsql
        private static DateTime LocalDate(int year, int month, int day, int hour = 12, int minute = 0)
        {
            return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local).ToUniversalTime();
        }
}
}
