namespace SoftwareEngineering.BLL.Models;

public class UserModel : BaseModel {
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class RegisterUserModel {
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginUserModel {
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}