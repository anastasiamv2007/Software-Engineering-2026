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
    private const string UnexpectedErrorMessage = "Сталася внутрішня помилка. Спробуйте ще раз.";

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
        try
        {
            // Параметри у {фігурних дужках} стають полями для фільтрації в Seq
            this.Logger.Information(
                "Спроба додати співвиконавця {Email} до проєкту {ProjectId} користувачем {UserId}",
                email,
                projectId,
                currentUserId);

            // 0. Перевірка вхідних даних
            var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!EmailRegex.IsMatch(normalizedEmail))
            {
                return this.Fail(ErrorType.Validation, "Введіть коректний email користувача.");
            }

            // 1. Перевірка: чи є currentUserId Owner-ом цього проєкту?
            var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
            if (currentRole != ProjectRole.Owner)
            {
                return this.Fail(ErrorType.Forbidden, "Тільки власник проєкту може додавати співвиконавців.");
            }

            // 2. Пошук: чи існує користувач з таким email?
            var targetUser = await this.userRepository.GetByEmailAsync(normalizedEmail);
            if (targetUser == null)
            {
                return this.Fail(ErrorType.NotFound, "Користувача з таким email не знайдено.");
            }

            // 3. Перевірка: чи цей email не належить самому Owner-у?
            if (targetUser.Id == currentUserId)
            {
                return this.Fail(ErrorType.Validation, "Ви не можете додати самого себе як співвиконавця.");
            }

            // 4. Перевірка: чи цей користувач вже не є учасником?
            var targetUserRole = await this.projectRepository.GetMemberRoleAsync(projectId, targetUser.Id);
            if (targetUserRole != null)
            {
                return this.Fail(ErrorType.Duplicate, "Цей користувач вже є учасником проєкту.");
            }

            // 5. Збереження: створення ProjectMember з роллю CoExecutor
            await this.projectRepository.AddMemberAsync(new ProjectMember
            {
                ProjectId = projectId,
                UserId = targetUser.Id,
                Role = ProjectRole.CoExecutor,
            });

            this.Logger.Information(
                "Користувача {TargetUserId} додано співвиконавцем до проєкту {ProjectId}",
                targetUser.Id,
                projectId);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час додавання співвиконавця до проєкту {ProjectId}", projectId);
            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }
    public async Task<Result<IReadOnlyList<ProjectMemberModel>>> GetMembersAsync(Guid projectId, Guid currentUserId)
    {
        try
        {
            if (projectId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return this.FailWith<IReadOnlyList<ProjectMemberModel>>(ErrorType.Validation, "Не вказано проєкт або користувача.");
            }

            var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
            if (currentRole is null)
            {
                return this.FailWith<IReadOnlyList<ProjectMemberModel>>(ErrorType.Forbidden, "Список учасників доступний лише учасникам проєкту.");
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
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час отримання учасників проєкту {ProjectId}", projectId);
            return Result.Fail<IReadOnlyList<ProjectMemberModel>>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    public async Task<Result> RemoveMemberAsync(Guid projectId, Guid memberId, Guid currentUserId)
    {
        try
        {
            this.Logger.Information(
                "Спроба видалити учасника {MemberId} з проєкту {ProjectId} користувачем {UserId}",
                memberId,
                projectId,
                currentUserId);

            if (projectId == Guid.Empty || memberId == Guid.Empty || currentUserId == Guid.Empty)
            {
                return this.FailWith(ErrorType.Validation, "Не вказано проєкт, учасника або користувача.");
            }

            // 1. Діє Owner
            var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
            if (currentRole != ProjectRole.Owner)
            {
                return this.FailWith(ErrorType.Forbidden, "Тільки власник проєкту може видаляти учасників.");
            }

            // 2. Учасник існує
            var memberRole = await this.projectRepository.GetMemberRoleAsync(projectId, memberId);
            if (memberRole is null)
            {
                return this.FailWith(ErrorType.NotFound, "Учасника в цьому проєкті не знайдено.");
            }

            // 3. Owner не видаляє сам себе
            if (memberId == currentUserId)
            {
                return this.FailWith(ErrorType.Validation, "Ви не можете видалити самого себе: спершу передайте власність.");
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
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час видалення учасника {MemberId} з проєкту {ProjectId}", memberId, projectId);
            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    private Result FailWith(ErrorType type, string message)
    {
        this.Logger.Warning("Операцію з учасниками відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail(type, message);
    }

    private Result<T> FailWith<T>(ErrorType type, string message)
    {
        this.Logger.Warning("Операцію з учасниками відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail<T>(type, message);
    }

    private Result Fail(ErrorType type, string message)
    {
        this.Logger.Warning("Додавання співвиконавця відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail(type, message);
    }

    public async Task<Result> TransferOwnershipAsync(Guid projectId, Guid newOwnerId, Guid currentUserId)
    {
        try
        {
            this.Logger.Information(
                "Спроба передати власність проєкту {ProjectId} від {OldOwnerId} до {NewOwnerId}",
                projectId,
                currentUserId,
                newOwnerId);

            // 1. Перевірка: чи діє поточний Owner проєкту?
            var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
            if (currentRole != ProjectRole.Owner)
            {
                return this.FailWith(ErrorType.Forbidden, "Тільки власник проєкту може передавати права власності.");
            }

            // 2. Перевірка: чи не передає він власність самому собі?
            if (newOwnerId == currentUserId)
            {
                return this.FailWith(ErrorType.Validation, "Ви не можете передати права власності самому собі.");
            }

            // 3. Перевірка: чи є новий власник уже учасником проєкту?
            var targetUserRole = await this.projectRepository.GetMemberRoleAsync(projectId, newOwnerId);
            if (targetUserRole == null)
            {
                return this.FailWith(ErrorType.Validation, "Новий власник повинен бути учасником цього проєкту.");
            }

            // 4. Виконання атомарної транзакції в DAL
            await this.projectRepository.TransferOwnershipAsync(projectId, newOwnerId);

            // 5. Логування у Seq згідно з вимогами документа №7
            this.Logger.Information(
                "Власність проєкту {ProjectId} передано {OldOwnerId} → {NewOwnerId}",
                projectId,
                currentUserId,
                newOwnerId);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            this.Logger.Error(
                ex,
                "Критична помилка під час передачі власності проєкту {ProjectId} від {OldOwnerId} до {NewOwnerId}",
                projectId,
                currentUserId,
                newOwnerId);
            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    public async Task<Result<IReadOnlyList<ProjectMemberModel>>> GetTransferCandidatesAsync(Guid projectId, Guid currentUserId)
    {
        try
        {
            var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
            if (currentRole != ProjectRole.Owner)
            {
                return this.FailWith<IReadOnlyList<ProjectMemberModel>>(
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
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Помилка отримання учасників для передачі власності проєкту {ProjectId}", projectId);
            return Result.Fail<IReadOnlyList<ProjectMemberModel>>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }
}
