using System.Security.Cryptography;
using System.Text;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Шифрує секрети через Windows DPAPI: розшифрувати їх може лише той самий користувач Windows на тому самому ПК.
/// </summary>
public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Planora.DbConnections.v1");

    public static string Protect(string secret)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Шифрування паролів підтримується лише у Windows.");
        }

        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Повертає null, якщо секрет пошкоджений або зашифрований іншим користувачем Windows.
    /// </summary>
    public static string? TryUnprotect(string protectedSecret)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            byte[] decrypted = ProtectedData.Unprotect(Convert.FromBase64String(protectedSecret), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
