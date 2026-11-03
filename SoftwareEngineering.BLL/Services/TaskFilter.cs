using Serilog;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Фільтрація та сортування завдань у пам'яті. Чиста логіка без БД — помилки повертаються через Result, а не винятками.
/// </summary>
public static class TaskFilter
{
    public static Result<IReadOnlyList<TaskModel>> Apply(IEnumerable<TaskModel> tasks, TaskFilterCriteria criteria)
    {
        if (tasks == null)
        {
            Log.Warning("TaskFilter.Apply отримав нульовий список завдань");
            return Result.Fail<IReadOnlyList<TaskModel>>(ErrorType.Validation, "Список завдань не може бути null.");
        }

        if (criteria == null)
        {
            Log.Warning("TaskFilter.Apply отримав нульові критерії фільтрації");
            return Result.Fail<IReadOnlyList<TaskModel>>(ErrorType.Validation, "Критерії фільтрації не можуть бути null.");
        }

        try
        {
            var query = tasks;
            var now = DateTime.UtcNow;

            // Фільтри поєднуються через AND
            if (criteria.Status.HasValue)
            {
                query = query.Where(t => t.Status == criteria.Status.Value);
            }

            if (criteria.Priority.HasValue)
            {
                query = query.Where(t => t.Priority == criteria.Priority.Value);
            }

            if (criteria.CategoryId.HasValue)
            {
                query = query.Where(t => t.CategoryId == criteria.CategoryId.Value);
            }

            // Прострочена: дедлайн минув, а статус не Done
            if (criteria.OnlyOverdue)
            {
                query = query.Where(t => t.Status != TaskItemStatus.Done &&
                                         t.Deadline.HasValue &&
                                         t.Deadline.Value.ToUniversalTime() < now);
            }

            query = criteria.SortBy switch
            {
                TaskSortOption.ByTitleAsc => query.OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase),

                // У enum TaskPriority High = 0, Low = 2, тому High → Low — це сортування за зростанням
                TaskSortOption.ByPriorityDesc => query.OrderBy(t => t.Priority),

                // Завдання без дедлайну завжди йдуть у кінець
                _ => query.OrderBy(t => t.Deadline.HasValue ? 0 : 1).ThenBy(t => t.Deadline),
            };

            var resultList = query.ToList();

            // Debug, бо фільтр викликається на кожну зміну пошуку — Information засмічував би Seq
            Log.Debug("Фільтрацію застосовано: знайдено {Count} завдань", resultList.Count);

            return Result.Ok<IReadOnlyList<TaskModel>>(resultList);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Помилка під час виконання TaskFilter.Apply");
            return Result.Fail<IReadOnlyList<TaskModel>>(ErrorType.Unexpected, "Не вдалося застосувати фільтр. Спробуйте ще раз.");
        }
    }
}
