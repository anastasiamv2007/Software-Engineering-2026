namespace SoftwareEngineering.DAL.Exceptions;

/// <summary>
/// Код завдання вже зайнятий у цьому проєкті (унікальний індекс project_id + task_code).
/// Буває, коли двоє одночасно створюють завдання; BLL у такому разі генерує код заново.
/// </summary>
public class DuplicateTaskCodeException : Exception
{
    public DuplicateTaskCodeException(string taskCode, Exception innerException)
        : base($"Код завдання {taskCode} уже зайнятий у цьому проєкті.", innerException)
    {
        this.TaskCode = taskCode;
    }

    public string TaskCode { get; }
}
