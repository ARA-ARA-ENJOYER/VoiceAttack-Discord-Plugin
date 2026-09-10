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

    private async Task InitiateCallWindows(string username)
    {
        var discordProcess = Process.GetProcessesByName("Discord").FirstOrDefault();
        if (discordProcess == null)
        {
            _va.WriteToLog("Discord: Discord desktop app not found. Please open Discord first.", "red");
            return;
        }

        try
        {
            if (discordProcess.MainWindowHandle == IntPtr.Zero)
            {
                _va.WriteToLog("Discord: Discord window not visible. Please open Discord first.", "red");
                return;
            }

            SetForegroundWindow(discordProcess.MainWindowHandle);
            await Task.Delay(500);

            SendKeys("^u");
            await Task.Delay(300);

            SendKeys(username);
            await Task.Delay(800);

            SendKeys("{ENTER}");
            await Task.Delay(500);

            SendKeys("^+c");
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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void SendKeys(string keys);
}
