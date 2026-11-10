using System;
using System.Threading.Tasks;
using Serilog;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;


namespace SoftwareEngineering.BLL.Services
{
    public class SubtaskService : ISubtaskService
    {
        private readonly ISubtaskRepository _repository;

        public SubtaskService(ISubtaskRepository repository)
        {
            _repository = repository;
        }

        public async Task<Result<SubtaskModel>> AddAsync(Guid taskId, string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                Log.Warning("Спроба створити порожню підзадачу. TaskId: {TaskId}", taskId);
                // default замість null прибирає варнінги компілятора
                return new Result<SubtaskModel>(default, false, new Error(ErrorType.Validation, "Назва підзадачі не може бути порожньою."));
            }

            if (title.Length > 200)
            {
                Log.Warning("Перевищено ліміт у 200 символів для підзадачі. TaskId: {TaskId}", taskId);
                return new Result<SubtaskModel>(default, false, new Error(ErrorType.Validation, "Назва підзадачі не повинна перевищувати 200 символів."));
            }

            try
            {
                var entity = new SubtaskEntity
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskId,
                    Title = title!.Trim(), // '!' гарантує компілятору, що title тут не null
                    IsCompleted = false
                };

                var createdEntity = await _repository.AddAsync(entity);

                var model = new SubtaskModel
                {
                    Id = createdEntity.Id,
                    TaskId = createdEntity.TaskId,
                    Title = createdEntity.Title,
                    IsCompleted = createdEntity.IsCompleted
                };

                Log.Information("Створено нову підзадачу {SubtaskId} для задачі {TaskId}", model.Id, taskId);
                // Передаємо default замість null для об'єкта Error, якщо успіх
                return new Result<SubtaskModel>(model, true, default);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Критична помилка при збереженні підзадачі для TaskId: {TaskId}", taskId);
                return new Result<SubtaskModel>(default, false, new Error(ErrorType.Unexpected, "Сталася помилка при збереженні в базу даних."));
            }
        }

        public async Task<Result<bool>> ToggleAsync(Guid id, bool currentStatus)
        {
            try
            {
                var newStatus = !currentStatus;
                await _repository.UpdateStatusAsync(id, newStatus);

                Log.Information("Статус підзадачі {SubtaskId} змінено на {NewStatus}", id, newStatus);
                return new Result<bool>(newStatus, true, default);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Помилка при зміні статусу підзадачі {SubtaskId}", id);
                return new Result<bool>(default, false, new Error(ErrorType.Unexpected, "Не вдалося оновити статус підзадачі."));
            }
        }

        public async Task<Result<bool>> DeleteAsync(Guid id)
        {
            try
            {
                await _repository.DeleteAsync(id);
                Log.Information("Підзадачу {SubtaskId} успішно видалено", id);
                return new Result<bool>(true, true, default);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Помилка при видаленні підзадачі {SubtaskId}", id);
                return new Result<bool>(default, false, new Error(ErrorType.Unexpected, "Не вдалося видалити підзадачу."));
            }
        }
    }
}