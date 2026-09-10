using System.Text;

namespace VoiceAttackDiscordPlugin;

/// <summary>
/// Keeps VoiceAttack logs and TTS variables single-line and bounded:
/// strips control characters (message bodies/DMs can contain anything)
/// and truncates runaway content.
/// </summary>
public static class LogSanitizer
{
    public static string Sanitize(string? text, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(char.IsControl(c) ? ' ' : c);

        string clean = sb.ToString().Trim();
        return clean.Length > maxLength ? clean.Substring(0, maxLength) + "…" : clean;
    }
}
