using System;
using System.Collections.Generic;
using System.Linq;
using Xunit; // Або NUnit.Framework, залежно від вашого фреймворку
using SoftwareEngineering.BLL.Models;
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
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
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
            var filteredTasks = result.Value!.ToList(); // Виправлено тут

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
            var sortedTasks = result.Value!.ToList(); // Виправлено тут

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
            var filteredTasks = result.Value!.ToList(); // Виправлено тут

            Assert.Single(filteredTasks);
            Assert.Equal("2", filteredTasks.First().Title);
        }
    }
}