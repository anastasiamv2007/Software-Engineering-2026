using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using SoftwareEngineering.BLL.Common;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Services
{
    public static class TaskFilter
    {
        public static Result<IEnumerable<TaskModel>> Apply(IEnumerable<TaskModel> tasks, TaskFilterCriteria criteria)
        {
            // 1. Перевірка на null замість throw (дотримуємося патерну Result)
            if (tasks == null)
            {
                Log.Warning("TaskFilter.Apply отримав нульовий список завдань (tasks is null).");
                return Result<IEnumerable<TaskModel>>.Failure("Список завдань не може бути null.");
            }

            if (criteria == null)
            {
                Log.Warning("TaskFilter.Apply отримав нульові критерії фільтрації (criteria is null).");
                return Result<IEnumerable<TaskModel>>.Failure("Критерії фільтрації не можуть бути null.");
            }

            try
            {
                var query = tasks.AsQueryable();
                var now = DateTime.UtcNow;

                // 2. Фільтрація (усі умови поєднуються через AND)

                // Статус
                if (criteria.Status.HasValue)
                {
                    query = query.Where(t => t.Status == criteria.Status.Value);
                }

                // Пріоритет
                if (criteria.Priority.HasValue)
                {
                    query = query.Where(t => t.Priority == criteria.Priority.Value);
                }

                // Категорія (якщо буде реалізовано в майбутньому)
                if (criteria.CategoryId.HasValue)
                {
                    // query = query.Where(t => t.CategoryId == criteria.CategoryId.Value);
                }

                // Тільки прострочені (Дедлайн минув, а статус НЕ Done)
                if (criteria.OnlyOverdue)
                {
                    query = query.Where(t => t.Status != TaskItemStatus.Done &&
                                             t.Deadline.HasValue &&
                                             t.Deadline.Value.ToUniversalTime() < now);
                }

                // 3. Сортування
                query = criteria.SortBy switch
                {
                    TaskSortOption.ByTitleAsc => query.OrderBy(t => t.Title),

                    TaskSortOption.ByPriorityDesc => query.OrderByDescending(t => t.Priority),

                    // Хитрість для дедлайну: null-значення завжди летять у кінець списку!
                    TaskSortOption.ByDeadlineAsc => query
                        .OrderBy(t => t.Deadline.HasValue ? 0 : 1)
                        .ThenBy(t => t.Deadline),

                    _ => query.OrderBy(t => t.Deadline)
                };

                var resultList = query.ToList();

                // Логуємо успіх у Seq
                Log.Information("Фільтрацію та сортування успішно застосовано. Знайдено завдань після фільтру: {Count}", resultList.Count);

                return Result<IEnumerable<TaskModel>>.Success(resultList);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Помилка під час виконання TaskFilter.Apply");
                return Result<IEnumerable<TaskModel>>.Failure($"Сталася системна помилка під час фільтрації: {ex.Message}");
            }
        }
    }
}