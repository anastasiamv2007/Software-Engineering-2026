using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class ProjectMemberService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ProjectMemberService> _logger;

    public ProjectMemberService(
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        ILogger<ProjectMemberService> logger)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result> AddCoExecutorAsync(Guid projectId, string email, Guid currentUserId)
    {
        // Логування для Seq: параметри в {ФігурнихДужках} автоматично стануть полями для фільтрації
        _logger.LogInformation("Attempting to add co-executor {Email} to project {ProjectId} by user {UserId}", email, projectId, currentUserId);

        // 1. Перевірка: чи є currentUserId Owner-ом цього проєкту?
        var currentRole = await _projectRepository.GetMemberRoleAsync(projectId, currentUserId);
        if (currentRole != ProjectRole.Owner)
        {
            _logger.LogWarning("User {UserId} failed to add co-executor. Reason: Not an owner of project {ProjectId}.", currentUserId, projectId);
            return Result.Fail(ErrorType.Forbidden, "Тільки власник проєкту може додавати співвиконавців.");
        }

        // 2. Пошук: чи існує користувач з таким email?
        var targetUser = await _userRepository.GetByEmailAsync(email);
        if (targetUser == null)
        {
            _logger.LogWarning("Failed to add co-executor. User with email {Email} not found.", email);
            return Result.Fail(ErrorType.NotFound, "Користувача з таким email не знайдено.");
        }

        // 3. Перевірка: чи цей email не належить самому Owner-у?
        if (targetUser.Id == currentUserId)
        {
            _logger.LogWarning("User {UserId} attempted to add themselves as co-executor to project {ProjectId}.", currentUserId, projectId);
            return Result.Fail(ErrorType.Validation, "Ви не можете додати самого себе як співвиконавця.");
        }

        // 4. Перевірка: чи цей користувач вже не є учасником?
        var targetUserRole = await _projectRepository.GetMemberRoleAsync(projectId, targetUser.Id);
        if (targetUserRole != null)
        {
            _logger.LogWarning("User {TargetUserId} is already a member of project {ProjectId}.", targetUser.Id, projectId);
            return Result.Fail(ErrorType.Duplicate, "Цей користувач вже є учасником проєкту.");
        }

        // 5. Збереження: створення ProjectMember з роллю CoExecutor
        var newMember = new ProjectMember
        {
            ProjectId = projectId,
            UserId = targetUser.Id,
            Role = ProjectRole.CoExecutor
        };

        await _projectRepository.AddMemberAsync(newMember);

        _logger.LogInformation("Successfully added user {TargetUserId} as CoExecutor to project {ProjectId}.", targetUser.Id, projectId);

        return Result.Ok();
    }
}