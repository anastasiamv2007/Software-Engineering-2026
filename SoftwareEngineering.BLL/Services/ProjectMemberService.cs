using System.Text.RegularExpressions;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

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

    public async Task<IReadOnlyList<ProjectMemberModel>> GetProjectMembersAsync(Guid projectId)
    {
        var members = await this.projectRepository.GetProjectMembersAsync(projectId);

        return members
            .Select(m => new ProjectMemberModel
            {
                ProjectId = m.ProjectId,
                UserId = m.UserId,
                FullName = m.User?.FullName ?? string.Empty,
                Email = m.User?.Email ?? string.Empty,
                Role = m.Role,
            })
            .ToList();
    }

    public async Task<Result> ChangeRoleAsync(Guid projectId, Guid memberId, ProjectRole newRole, Guid currentUserId)
    {
        try
        {
            // 1. Перевірка: діє Owner
            var currentRole = await this.projectRepository.GetMemberRoleAsync(projectId, currentUserId);
            if (currentRole != ProjectRole.Owner)
            {
                return this.Fail(ErrorType.Forbidden, "Тільки власник проєкту може змінювати ролі учасників.");
            }

            // 2. Перевірка: змінювати свою роль не можна
            if (memberId == currentUserId)
            {
                return this.Fail(ErrorType.Validation, "Ви не можете змінювати власну роль.");
            }

            // 3. Перевірка: нова роль лише CoExecutor або Reviewer (Owner передається лише через UC-3)
            if (newRole != ProjectRole.CoExecutor && newRole != ProjectRole.Reviewer)
            {
                return this.Fail(ErrorType.Validation, "Нова роль може бути лише Співвиконавець або Читач.");
            }

            // Отримуємо поточну роль учасника
            var oldRole = await this.projectRepository.GetMemberRoleAsync(projectId, memberId);
            if (oldRole == null)
            {
                return this.Fail(ErrorType.NotFound, "Учасника не знайдено у цьому проєкті.");
            }

            if (oldRole == ProjectRole.Owner)
            {
                return this.Fail(ErrorType.Validation, "Неможливо змінити роль власника проєкту.");
            }

            // 4. Якщо роль та сама — базу не змінюємо
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
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час зміни ролі учасника {MemberId} у проєкті {ProjectId}", memberId, projectId);
            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    private Result Fail(ErrorType type, string message)
    {
        this.Logger.Warning("Додавання співвиконавця відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail(type, message);
    }


}
