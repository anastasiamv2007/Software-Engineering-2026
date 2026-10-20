using System;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Services;

public class UserSession : IUserSession {
    public UserModel? CurrentUser { get; private set; }
    public Guid? CurrentUserId => CurrentUser?.Id;
    public bool IsAuthenticated => CurrentUser != null;

    public void SetUser(UserModel user) {
        CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
    }

    public void Clear() {
        CurrentUser = null;
    }
}