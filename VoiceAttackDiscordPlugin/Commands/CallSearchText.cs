namespace VoiceAttackDiscordPlugin.Commands;

/// <summary>
/// Display names are not unique on Discord, usernames are.
/// Shared display name -> navigate the Quick Switcher by @username so it
/// lands on the right DM; otherwise the display name is friendlier.
/// </summary>
public static class CallSearchText
{
    public static string Select(string displayName, int nameMatches, string username)
        => nameMatches > 1 ? "@" + username : displayName;
}
