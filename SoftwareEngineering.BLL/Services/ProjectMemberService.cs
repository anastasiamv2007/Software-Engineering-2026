using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;
using System.Text.RegularExpressions;

namespace SoftwareEngineering.BLL.Services;

public class ProjectMemberService : BaseService, IProjectMemberService
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IProjectRepository projectRepository;
    private readonly IUserRepository userRepository;

    public ProjectMemberService(IProjectRepository projectRepository, IUserRepository userRepository)
    {
        this.projectRepository = projectRepository;
        this.userRepository = userRepository;
    }

    public async Task<Result> AddCoExecutorAsync(Guid projectId, string email, Guid currentUserId)
    {
        // 0. Перевірка вхідних даних
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!EmailRegex.IsMatch(normalizedEmail))
        {
            return this.Fail(ErrorType.Validation, "Введіть коректний email користувача.");
        }

        // 1. Діє Owner проєкту
        var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
        if (currentRole != ProjectRole.Owner)
        {
            return this.Fail(ErrorType.Forbidden, "Тільки власник проєкту може додавати співвиконавців.");
        }

        // 2. Користувач з таким email існує
        var targetUser = await this.userRepository.GetByEmailAsync(normalizedEmail);
        if (targetUser == null)
        {
            return this.Fail(ErrorType.NotFound, "Користувача з таким email не знайдено.");
        }

        // 3. Email не належить самому Owner-у
        if (targetUser.Id == currentUserId)
        {
            return this.Fail(ErrorType.Validation, "Ви не можете додати самого себе як співвиконавця.");
        }

        // 4. Користувач ще не учасник
        var targetUserRole = await this.projectRepository.GetMemberRoleAsync(projectId, targetUser.Id);
        if (targetUserRole != null)
        {
            return this.Fail(ErrorType.Duplicate, "Цей користувач вже є учасником проєкту.");
        }

        // 5. Збереження: ProjectMember з роллю CoExecutor
        await this.projectRepository.AddMemberAsync(new ProjectMember
        {
            ProjectId = projectId,
            UserId = targetUser.Id,
            Role = ProjectRole.CoExecutor,
        });

        // Параметри у {фігурних дужках} стають полями для фільтрації в Seq
        this.Logger.Information(
            "Користувач {UserId} додав співвиконавця {TargetUserId} ({Email}) до проєкту {ProjectId}",
            currentUserId,
            targetUser.Id,
            normalizedEmail,
            projectId);

        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<ProjectMemberModel>>> GetMembersAsync(Guid projectId, Guid currentUserId)
    {
        if (projectId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return this.Fail<IReadOnlyList<ProjectMemberModel>>(ErrorType.Validation, "Не вказано проєкт або користувача.");
        }

        var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
        if (currentRole is null)
        {
            return this.Fail<IReadOnlyList<ProjectMemberModel>>(ErrorType.Forbidden, "Список учасників доступний лише учасникам проєкту.");
        }

        var members = await this.projectRepository.GetMembersAsync(projectId);
        var models = members
            .Select(member => new ProjectMemberModel
            {
                UserId = member.UserId,
                FullName = member.User.FullName,
                Email = member.User.Email,
                Role = member.Role,
            })
            .ToList();

        // Старі проєкти могли не мати запису про власника в project_members
        if (models.All(model => model.Role != ProjectRole.Owner))
        {
            var project = await this.projectRepository.GetByIdAsync(projectId);
            if (project?.Owner is not null)
            {
                models.Add(new ProjectMemberModel
                {
                    UserId = project.OwnerId,
                    FullName = project.Owner.FullName,
                    Email = project.Owner.Email,
                    Role = ProjectRole.Owner,
                });
            }
        }

        IReadOnlyList<ProjectMemberModel> sorted = models
            .OrderBy(model => model.Role)
            .ThenBy(model => model.FullName)
            .ToList();

        return Result.Ok(sorted);
    }

    public async Task<Result> RemoveMemberAsync(Guid projectId, Guid memberId, Guid currentUserId)
    {
        if (projectId == Guid.Empty || memberId == Guid.Empty || currentUserId == Guid.Empty)
        {
            return this.Fail(ErrorType.Validation, "Не вказано проєкт, учасника або користувача.");
        }

        // 1. Діє Owner
        var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
        if (currentRole != ProjectRole.Owner)
        {
            return this.Fail(ErrorType.Forbidden, "Тільки власник проєкту може видаляти учасників.");
        }

        // 2. Учасник існує
        var memberRole = await this.projectRepository.GetMemberRoleAsync(projectId, memberId);
        if (memberRole is null)
        {
            return this.Fail(ErrorType.NotFound, "Учасника в цьому проєкті не знайдено.");
        }

        // 3. Owner не видаляє сам себе
        if (memberId == currentUserId)
        {
            return this.Fail(ErrorType.Validation, "Ви не можете видалити самого себе: спершу передайте власність.");
        }

        // 4. Одна транзакція в репозиторії: учасник, виконавець у задачах, власник задач
        var reassigned = await this.projectRepository.RemoveMemberAsync(projectId, memberId);

        this.Logger.Information(
            "Owner {UserId} видалив учасника {MemberId} з проєкту {ProjectId}; перепризначено задач: {Count}",
            currentUserId,
            memberId,
            projectId,
            reassigned);

        return Result.Ok();
    }

    public async Task<Result> ChangeRoleAsync(Guid projectId, Guid memberId, ProjectRole newRole, Guid currentUserId)
    {
        // 1. Діє Owner
        var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
        if (currentRole != ProjectRole.Owner)
        {
            return this.Fail(ErrorType.Forbidden, "Тільки власник проєкту може змінювати ролі учасників.");
        }

        // 2. Свою роль змінювати не можна
        if (memberId == currentUserId)
        {
            return this.Fail(ErrorType.Validation, "Ви не можете змінювати власну роль.");
        }

        // 3. Owner передається лише через «Передати власність»
        if (newRole is not (ProjectRole.CoExecutor or ProjectRole.Reviewer))
        {
            return this.Fail(ErrorType.Validation, "Нова роль може бути лише Співвиконавець або Читач.");
        }

        var oldRole = await this.projectRepository.GetMemberRoleAsync(projectId, memberId);
        if (oldRole is null)
        {
            return this.Fail(ErrorType.NotFound, "Учасника не знайдено у цьому проєкті.");
        }

        if (oldRole == ProjectRole.Owner)
        {
            return this.Fail(ErrorType.Validation, "Неможливо змінити роль власника проєкту.");
        }

        // 4. Роль та сама — базу не змінюємо
        if (oldRole == newRole)
        {
            return Result.Ok();
        }

        await this.projectRepository.UpdateMemberRoleAsync(projectId, memberId, newRole);

        this.Logger.Information(
            "Роль {MemberId} у проєкті {ProjectId} змінено {OldRole} → {NewRole}",
            memberId,
            projectId,
            oldRole.Value,
            newRole);

        return Result.Ok();
    }

    public async Task<Result> TransferOwnershipAsync(Guid projectId, Guid newOwnerId, Guid currentUserId)
    {
        // 1. Діє поточний Owner проєкту
        var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
        if (currentRole != ProjectRole.Owner)
        {
            return this.Fail(ErrorType.Forbidden, "Тільки власник проєкту може передавати права власності.");
        }

        // 2. Не самому собі
        if (newOwnerId == currentUserId)
        {
            return this.Fail(ErrorType.Validation, "Ви не можете передати права власності самому собі.");
        }

        // 3. Новий власник уже є учасником проєкту
        var targetUserRole = await this.projectRepository.GetMemberRoleAsync(projectId, newOwnerId);
        if (targetUserRole == null)
        {
            return this.Fail(ErrorType.Validation, "Новий власник повинен бути учасником цього проєкту.");
        }

        // 4. Атомарна транзакція в DAL
        if (!await this.projectRepository.TransferOwnershipAsync(projectId, newOwnerId))
        {
            // Між перевіркою і транзакцією учасника встигли вилучити або проєкт видалили
            return this.Fail(ErrorType.NotFound, "Проєкт або нового власника не знайдено. Оновіть список учасників.");
        }

        this.Logger.Information(
            "Власність проєкту {ProjectId} передано {OldOwnerId} → {NewOwnerId}",
            projectId,
            currentUserId,
            newOwnerId);

        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<ProjectMemberModel>>> GetTransferCandidatesAsync(Guid projectId, Guid currentUserId)
    {
        var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
        if (currentRole != ProjectRole.Owner)
        {
            return this.Fail<IReadOnlyList<ProjectMemberModel>>(
                ErrorType.Forbidden,
                "Тільки власник проєкту може переглядати кандидатів для передачі прав.");
        }

        // Той самий список учасників, що й на сторінці проєкту, але без самого власника
        var members = await this.projectRepository.GetMembersAsync(projectId);
        IReadOnlyList<ProjectMemberModel> candidates = members
            .Where(member => member.UserId != currentUserId)
            .Select(member => new ProjectMemberModel
            {
                UserId = member.UserId,
                FullName = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email : member.User.FullName,
                Email = member.User.Email,
                Role = member.Role,
            })
            .ToList();

        return Result.Ok(candidates);
    }
}
