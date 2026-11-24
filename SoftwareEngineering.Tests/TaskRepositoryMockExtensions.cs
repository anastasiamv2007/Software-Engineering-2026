using Moq;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

/// <summary>
/// Справжній TaskRepository.AddAsync сам читає наявні коди проєкту і викликає генератор від сервісу.
/// Мок робить те саме з переданим списком кодів, щоб тести бачили той код, який отримало б завдання.
/// </summary>
internal static class TaskRepositoryMockExtensions
{
    public static void SetupAdd(
        this Mock<ITaskRepository> mock,
        IReadOnlyList<string>? existingCodes = null,
        Action<TaskItem>? onSaved = null)
    {
        mock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<Func<IReadOnlyList<string>, string>>()))
            .Callback<TaskItem, Func<IReadOnlyList<string>, string>>((task, generateCode) =>
            {
                task.TaskCode = generateCode(existingCodes ?? Array.Empty<string>());
                onSaved?.Invoke(task);
            })
            .Returns(Task.CompletedTask);
    }
}
