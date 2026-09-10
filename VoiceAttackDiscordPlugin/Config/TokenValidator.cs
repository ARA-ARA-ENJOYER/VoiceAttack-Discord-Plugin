using System.Text.RegularExpressions;

namespace VoiceAttackDiscordPlugin.Config;

/// <summary>
/// Format check for Discord bot tokens (three dot-separated base64url-ish segments).
/// This is a plausibility check only — real validation happens against Discord's API.
/// </summary>
public static class TokenValidator
{
    private static readonly Regex TokenPattern = new(@"^[\w-]+\.[\w-]+\.[\w-]+$", RegexOptions.Compiled);

    public static bool IsPlausible(string? token)
        => !string.IsNullOrWhiteSpace(token) && TokenPattern.IsMatch(token.Trim());
}
