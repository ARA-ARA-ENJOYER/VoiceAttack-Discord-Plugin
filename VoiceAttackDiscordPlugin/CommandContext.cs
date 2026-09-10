namespace VoiceAttackDiscordPlugin;

/// <summary>
/// Parsed V4 plugin context: "action:arg1:arg2" (colon-delimited;
/// extra colons stay in Arg3 so messages may contain colons).
/// </summary>
public sealed record CommandContext(string Action, string Arg1, string Arg2, string Arg3)
{
    public static CommandContext Parse(string? fullContext)
    {
        var parts = (fullContext ?? "").Split(new[] { ':' }, StringSplitOptions.None);

        string action = parts.Length > 0 ? parts[0].Trim() : "";
        string arg1 = parts.Length > 1 ? parts[1].Trim() : "";
        string arg2 = parts.Length > 2 ? parts[2].Trim() : "";
        string arg3 = parts.Length > 3 ? string.Join(":", parts.Skip(3)).Trim() : "";

        return new CommandContext(action, arg1, arg2, arg3);
    }
}
