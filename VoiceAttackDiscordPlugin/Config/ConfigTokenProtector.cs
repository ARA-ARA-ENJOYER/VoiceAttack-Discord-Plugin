using System.Security.Cryptography;
using System.Text;

namespace VoiceAttackDiscordPlugin.Config;

/// <summary>
/// DPAPI (CurrentUser scope) wrapper for the bot token at rest.
/// Windows-only; both the plugin and the setup wizard run on Windows.
/// The protected blob is bound to this Windows user on this machine —
/// a config.json copied elsewhere must be re-seeded via the wizard.
/// </summary>
public static class ConfigTokenProtector
{
    public static string Protect(string plaintext)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI token protection requires Windows.");
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] shielded = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(shielded);
    }

    public static string Unprotect(string base64)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI token protection requires Windows.");
        byte[] shielded = Convert.FromBase64String(base64);
        byte[] data = ProtectedData.Unprotect(shielded, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(data);
    }
}
