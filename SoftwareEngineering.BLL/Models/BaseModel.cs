using System;

namespace SoftwareEngineering.BLL.Models;

/// <summary>
/// Базова модель для об'єктів бізнес-логіки (DTO / Domain Models).
/// </summary>
public abstract class BaseModel
{
    public Guid Id { get; set; }
}