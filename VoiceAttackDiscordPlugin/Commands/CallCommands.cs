using System.Runtime.InteropServices;

namespace VoiceAttackDiscordPlugin.Commands;

public class CallCommands
{
    private readonly DiscordBotManager _botManager;
    private readonly dynamic _va;

    public CallCommands(DiscordBotManager botManager, dynamic va)
    {
        _botManager = botManager;
        _va = va;
    }

    public async Task CallUserAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            _va.WriteToLog("Discord: No user name provided for calluser.", "yellow");
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
            _va.WriteToLog($"Discord: User '{userName}' not found for call.", "red");
            return;
        }

        _va.WriteToLog($"Discord: Initiating call to '{user.Username}' via keyboard automation...", "blue");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await InitiateCallWindows(user.Username);
            }
            else
            {
                _va.WriteToLog("Discord: Call automation only supported on Windows.", "red");
            }
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Call initiation failed: {ex.Message}", "red");
        }
    }

    public async Task CallUserByIdAsync(string userIdStr)
    {
        if (string.IsNullOrWhiteSpace(userIdStr) || !ulong.TryParse(userIdStr.Trim(), out ulong userId))
        {
            _va.WriteToLog("Discord: Invalid user ID provided for callbyid.", "yellow");
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
            _va.WriteToLog($"Discord: User ID '{userId}' not found for call.", "red");
            return;
        }

        // ID lookup is name-change proof; IDs are unique while display names are not —
        // @username is an exact Quick Switcher hit
        string displayName = user.GlobalName ?? user.Username;
        _va.WriteToLog($"Discord: Initiating call to '{displayName}' (ID: {user.Id}) via keyboard automation...", "blue");
        string searchText = "@" + user.Username;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await InitiateCallByIdWindows(searchText, displayName);
            }
            else
            {
                _va.WriteToLog("Discord: Call automation only supported on Windows.", "red");
            }
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Call initiation failed: {ex.Message}", "red");
        }
    }

    public async Task CallByUsernameAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            _va.WriteToLog("Discord: No user name provided for callbyusername.", "yellow");
            return;
        }

        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        var user = await _botManager.FindUserByUsernameAsync(userName);
        if (user == null)
        {
            _va.WriteToLog($"Discord: Warning: no user found for '{userName}'. They may have changed their username — try callbyid:<ID> instead.", "yellow");
            return;
        }

        // Usernames are unique, so navigate by raw username for an exact Quick Switcher hit
        string displayName = user.GlobalName ?? user.Username;
        _va.WriteToLog($"Discord: Initiating call to '{displayName}' via keyboard automation...", "blue");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await InitiateCallByIdWindows(user.Username, displayName);
            }
            else
            {
                _va.WriteToLog("Discord: Call automation only supported on Windows.", "red");
            }
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Call initiation failed: {ex.Message}", "red");
        }
    }

    public async Task CallByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _va.WriteToLog("Discord: No name provided for callbyname.", "yellow");
            return;
        }

        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        var user = await _botManager.FindUserAsync(name);
        if (user == null)
        {
            _va.WriteToLog($"Discord: Warning: no user found for '{name}'. They may have changed their display name — try callbyid:<ID> instead.", "yellow");
            return;
        }

        // Usernames are unique; display names are not. If the display name is shared,
        // navigate by @username instead so the Quick Switcher lands on the right DM.
        string displayName = user.GlobalName ?? user.Username;
        _va.WriteToLog($"Discord: Initiating call to '{displayName}' via keyboard automation...", "blue");

        int nameMatches = await _botManager.CountDistinctUsersByDisplayNameAsync(displayName);
        string searchText = CallSearchText.Select(displayName, nameMatches, user.Username);
        if (nameMatches > 1)
        {
            _va.WriteToLog($"Discord: Warning: {nameMatches} users share the display name '{displayName}'. Navigating by unique username '@{user.Username}' instead.", "yellow");
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await InitiateCallByIdWindows(searchText, displayName);
            }
            else
            {
                _va.WriteToLog("Discord: Call automation only supported on Windows.", "red");
            }
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Call initiation failed: {ex.Message}", "red");
        }
    }

    private async Task InitiateCallWindows(string username)
    {
        var discordHwnd = KeyboardAutomation.FindDiscordWindow(_va);
        if (discordHwnd == IntPtr.Zero) return;

        try
        {
            KeyboardAutomation.SetForegroundWindow(discordHwnd);
            await Task.Delay(500);

            KeyboardAutomation.PressCombo(KeyboardAutomation.VK_CONTROL, KeyboardAutomation.VkFor('u'));
            await Task.Delay(300);

            await KeyboardAutomation.TypeTextAsync(username);
            await Task.Delay(800);

            KeyboardAutomation.PressKey(KeyboardAutomation.VK_RETURN);
            await Task.Delay(500);

            KeyboardAutomation.PressCombo(KeyboardAutomation.VK_CONTROL, KeyboardAutomation.VK_SHIFT, KeyboardAutomation.VkFor('c'));
            await Task.Delay(300);

            _va.WriteToLog($"Discord: Call to '{username}' initiated via keyboard shortcuts.", "green");
            _va.WriteToLog("Discord: Note: This uses Ctrl+U -> search -> Enter -> Ctrl+Shift+C.", "yellow");
            _va.WriteToLog("Discord: If Discord's UI has changed, shortcuts may not work correctly.", "yellow");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Keyboard automation error: {ex.Message}", "red");
        }
    }

    private async Task InitiateCallByIdWindows(string searchText, string displayName)
    {
        var discordHwnd = KeyboardAutomation.FindDiscordWindow(_va);
        if (discordHwnd == IntPtr.Zero) return;

        try
        {
            KeyboardAutomation.SetForegroundWindow(discordHwnd);
            await Task.Delay(500);

            KeyboardAutomation.PressCombo(KeyboardAutomation.VK_CONTROL, KeyboardAutomation.VkFor('k'));
            await Task.Delay(400);

            await KeyboardAutomation.TypeTextAsync(searchText);
            await Task.Delay(800);

            KeyboardAutomation.PressKey(KeyboardAutomation.VK_RETURN);
            await Task.Delay(1000);

            KeyboardAutomation.PressCombo(KeyboardAutomation.VK_CONTROL, KeyboardAutomation.VkFor('\''));
            await Task.Delay(300);

            _va.WriteToLog($"Discord: Call to '{displayName}' initiated via keyboard shortcuts.", "green");
            _va.WriteToLog("Discord: Note: This uses Ctrl+K -> DM -> Ctrl+' (call).", "yellow");
            _va.WriteToLog("Discord: If Discord's UI has changed, shortcuts may not work correctly.", "yellow");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Keyboard automation error: {ex.Message}", "red");
        }
    }
}
