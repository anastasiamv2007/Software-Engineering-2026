using System;
using System.Threading.Tasks;
using Serilog;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class CommentService
{
    private readonly ICommentRepository _commentRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;

    public CommentService(
        ICommentRepository commentRepository,
        ITaskRepository taskRepository,
        IProjectRepository projectRepository)
    {
        _commentRepository = commentRepository;
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Result> AddCommentAsync(Guid taskId, Guid authorId, string content)
    {
        // 1. Валідація тексту (не порожній)
        if (string.IsNullOrWhiteSpace(content))
        {
            Log.Warning("Спроба додати порожній коментар. TaskId: {TaskId}, UserId: {UserId}", taskId, authorId);
            return Result.Fail(ErrorType.Validation, "Коментар не може бути порожнім.");
        }

        // 2. Валідація довжини (макс 1000 символів)
        if (content.Length > 1000)
        {
            Log.Warning("Перевищено ліміт символів у коментарі. TaskId: {TaskId}, Length: {Length}", taskId, content.Length);
            return Result.Fail(ErrorType.Validation, "Коментар не може перевищувати 1000 символів.");
        }

        // 3. Перевірка чи існує задача
        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            Log.Warning("Задачу не знайдено. TaskId: {TaskId}", taskId);
            return Result.Fail(ErrorType.NotFound, "Задачу не знайдено.");
        }

        // 4. Перевірка доступу (чи є автор учасником проєкту)
        var role = await _projectRepository.GetMemberRoleAsync(task.ProjectId, authorId);
        if (role == null)
        {
            Log.Warning("Користувач {UserId} не є учасником проєкту {ProjectId} для задачі {TaskId}", authorId, task.ProjectId, taskId);
            return Result.Fail(ErrorType.Forbidden, "Ви не є учасником проєкту цієї задачі.");
        }

        try
        {
            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                AuthorId = authorId,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _commentRepository.AddAsync(comment);

            Log.Information("Коментар успішно додано. CommentId: {CommentId}, TaskId: {TaskId}", comment.Id, taskId);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Критична помилка при додаванні коментаря. TaskId: {TaskId}", taskId);
            return Result.Fail(ErrorType.Unexpected, "Сталася системна помилка при збереженні коментаря.");
        }
    }
    public async Task<IEnumerable<SoftwareEngineering.DAL.Entities.Comment>> GetTaskCommentsAsync(Guid taskId, Guid projectId)
    {
        return await _commentRepository.GetByTaskIdWithAuthorInfoAsync(taskId, projectId);
    }
}