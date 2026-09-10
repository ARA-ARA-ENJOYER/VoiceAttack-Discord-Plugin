using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DiscordVAPlugin.Commands;

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

        // ID lookup is name-change proof; resolve the current display name for keyboard navigation
        string displayName = user.GlobalName ?? user.Username;
        _va.WriteToLog($"Discord: Initiating call to '{displayName}' (ID: {user.Id}) via keyboard automation...", "blue");

        // Usernames are unique; display names are not. If the display name is shared,
        // navigate by @username instead so the Quick Switcher lands on the right DM.
        string searchText = displayName;
        int nameMatches = await _botManager.CountDistinctUsersByDisplayNameAsync(displayName);
        if (nameMatches > 1)
        {
            _va.WriteToLog($"Discord: Warning: {nameMatches} users share the display name '{displayName}'. Navigating by unique username '@{user.Username}' instead.", "yellow");
            searchText = "@" + user.Username;
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

        var user = await _botManager.FindUserAsync(userName);
        if (user == null)
        {
            _va.WriteToLog($"Discord: User '{userName}' not found for call.", "red");
            return;
        }

        // Usernames are unique, so navigate by @username for an exact Quick Switcher hit
        string displayName = user.GlobalName ?? user.Username;
        _va.WriteToLog($"Discord: Initiating call to '{displayName}' via keyboard automation...", "blue");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await InitiateCallByIdWindows("@" + user.Username, displayName);
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
        var discordHwnd = FindDiscordWindow();
        if (discordHwnd == IntPtr.Zero) return;

        try
        {
            SetForegroundWindow(discordHwnd);
            await Task.Delay(500);

            PressCombo(VK_CONTROL, VkFor('u'));
            await Task.Delay(300);

            await TypeTextAsync(username);
            await Task.Delay(800);

            PressKey(VK_RETURN);
            await Task.Delay(500);

            PressCombo(VK_CONTROL, VK_SHIFT, VkFor('c'));
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
        var discordHwnd = FindDiscordWindow();
        if (discordHwnd == IntPtr.Zero) return;

        try
        {
            SetForegroundWindow(discordHwnd);
            await Task.Delay(500);

            // Quick switcher -> jump straight to the user's DM
            PressCombo(VK_CONTROL, VkFor('k'));
            await Task.Delay(400);

            await TypeTextAsync(searchText);
            await Task.Delay(800);

            PressKey(VK_RETURN);
            await Task.Delay(1000);

            // Start the voice call in the open DM
            PressCombo(VK_CONTROL, VkFor('\''));
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

    private IntPtr FindDiscordWindow()
    {
        var discordProcess = Process.GetProcessesByName("Discord").FirstOrDefault();
        if (discordProcess == null)
        {
            _va.WriteToLog("Discord: Discord desktop app not found. Please open Discord first.", "red");
            return IntPtr.Zero;
        }

        if (discordProcess.MainWindowHandle == IntPtr.Zero)
        {
            _va.WriteToLog("Discord: Discord window not visible. Please open Discord first.", "red");
            return IntPtr.Zero;
        }

        return discordProcess.MainWindowHandle;
    }

    // --- Low-level keyboard helpers (user32 keybd_event; the old SendKeys import does not exist) ---

    private const byte VK_CONTROL = 0x11;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_RETURN = 0x0D;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    private static byte VkFor(char ch)
    {
        // Layout-aware virtual-key code for a character (low byte of VkKeyScan result)
        return (byte)(VkKeyScan(ch) & 0xFF);
    }

    private static void KeyDown(byte vk) => keybd_event(vk, 0, 0, UIntPtr.Zero);

    private static void KeyUp(byte vk) => keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

    private static void PressKey(byte vk)
    {
        KeyDown(vk);
        KeyUp(vk);
    }

    private static void PressCombo(params byte[] vks)
    {
        foreach (byte vk in vks) KeyDown(vk);
        for (int i = vks.Length - 1; i >= 0; i--) KeyUp(vks[i]);
    }

    private static async Task TypeTextAsync(string text)
    {
        foreach (char ch in text)
        {
            short scan = VkKeyScan(ch);
            if (scan == -1) continue; // no key mapping on this layout; skip

            byte vk = (byte)(scan & 0xFF);
            bool shift = (scan & 0x100) != 0;

            if (shift) KeyDown(VK_SHIFT);
            PressKey(vk);
            if (shift) KeyUp(VK_SHIFT);

            await Task.Delay(15);
        }
    }
}
