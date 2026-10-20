using System;
using SoftwareEngineering.BLL.Models;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IUserSession {
    UserModel? CurrentUser { get; }
    Guid? CurrentUserId { get; }
    bool IsAuthenticated { get; }

    void SetUser(UserModel user);
    void Clear();
}