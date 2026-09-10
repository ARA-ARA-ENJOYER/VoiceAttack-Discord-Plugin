namespace DiscordVAPlugin.Commands;

public class UserCommands
{
    private readonly DiscordBotManager _botManager;
    private readonly dynamic _va;

    public UserCommands(DiscordBotManager botManager, dynamic va)
    {
        _botManager = botManager;
        _va = va;
    }

    public async Task SearchUserAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            _va.WriteToLog("Discord: No user name provided for searchuser.", "yellow");
            return;
        }

        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        var user = await _botManager.FindUserAsync(userName);
        if (user == null)
        {
            _va.WriteToLog($"Discord: User '{userName}' not found.", "red");
            _va.SetVar("Discord.found.UserId", "");
            _va.SetVar("Discord.found.DisplayName", "");
            _va.SetVar("Discord.found.Username", "");
            return;
        }

        _va.SetVar("Discord.found.UserId", user.Id.ToString());
        _va.SetVar("Discord.found.DisplayName", user.GlobalName ?? user.Username);
        _va.SetVar("Discord.found.Username", user.Username);
        _va.WriteToLog($"Discord: Found user '{user.Username}' (ID: {user.Id}).", "green");
    }

    public async Task SearchUserIdAsync(string userIdStr)
    {
        if (string.IsNullOrWhiteSpace(userIdStr) || !ulong.TryParse(userIdStr.Trim(), out ulong userId))
        {
            _va.WriteToLog("Discord: Invalid user ID provided for searchuserid.", "yellow");
            return;
        }

        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        var user = await _botManager.FindUserByIdAsync(userId);
        if (user == null)
        {
            _va.WriteToLog($"Discord: User ID '{userId}' not found.", "red");
            _va.SetVar("Discord.found.UserId", "");
            _va.SetVar("Discord.found.DisplayName", "");
            _va.SetVar("Discord.found.Username", "");
            return;
        }

        _va.SetVar("Discord.found.UserId", user.Id.ToString());
        _va.SetVar("Discord.found.DisplayName", user.GlobalName ?? user.Username);
        _va.SetVar("Discord.found.Username", user.Username);
        _va.WriteToLog($"Discord: Found user '{user.Username}' (ID: {user.Id}).", "green");
    }

    public async Task ListUsersAsync(string channelName)
    {
        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        if (string.IsNullOrWhiteSpace(channelName))
        {
            _va.WriteToLog("Discord: No channel name provided for listusers.", "yellow");
            return;
        }

        var users = await _botManager.GetChannelUsersAsync(channelName);
        if (users.Count == 0)
        {
            _va.WriteToLog($"Discord: No users found in channel '{channelName}'.", "yellow");
            _va.SetVar("Discord.Users", "");
            return;
        }

        var userList = string.Join(", ", users.Select(u => u.Username));
        _va.SetVar("Discord.Users", userList);
        _va.SetVar("Discord.UserCount", users.Count.ToString());
        _va.WriteToLog($"Discord: {users.Count} users in #{channelName}.", "green");
    }
}
