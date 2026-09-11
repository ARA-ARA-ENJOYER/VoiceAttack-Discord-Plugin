namespace VoiceAttackDiscordPlugin.Commands;

using System.Runtime.InteropServices;

public class VoiceCommands
{
    private readonly DiscordBotManager _botManager;
    private readonly dynamic _va;

    public VoiceCommands(DiscordBotManager botManager, dynamic va)
    {
        _botManager = botManager;
        _va = va;
    }

    public async Task JoinVoiceAsync(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            channelName = _botManager.DefaultChannelName;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            _va.WriteToLog("Discord: No voice channel name provided for joinvoice (and no DefaultChannelName configured).", "yellow");
            return;
        }

        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        var channel = await _botManager.JoinVoiceChannelAsync(channelName);
        if (channel != null)
        {
            _va.SetText("Discord.VoiceChannel", channel.Name);
        }
    }

    public async Task LeaveVoiceAsync()
    {
        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected.", "red");
            return;
        }

        await _botManager.LeaveVoiceChannelAsync();
        _va.SetText("Discord.VoiceChannel", "");
    }

    public async Task ToggleUserMuteAsync()
    {
        await ToggleUserKeyAsync('m', "mute");
    }

    public async Task ToggleUserDeafenAsync()
    {
        await ToggleUserKeyAsync('d', "deafen");
    }

    private async Task ToggleUserKeyAsync(char key, string label)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _va.WriteToLog($"Discord: {label} automation only supported on Windows.", "red");
            return;
        }

        var discordHwnd = KeyboardAutomation.FindDiscordWindow(_va);
        if (discordHwnd == IntPtr.Zero) return;

        try
        {
            KeyboardAutomation.SetForegroundWindow(discordHwnd);
            await Task.Delay(500);

            KeyboardAutomation.PressCombo(
                KeyboardAutomation.VK_CONTROL,
                KeyboardAutomation.VK_SHIFT,
                KeyboardAutomation.VkFor(key));
            await Task.Delay(300);

            _va.WriteToLog($"Discord: {label} toggled for you (Ctrl+Shift+{char.ToUpperInvariant(key)}).", "green");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Keyboard automation error: {ex.Message}", "red");
        }
    }

    public async Task ToggleBotMuteAsync()
    {
        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected.", "red");
            return;
        }

        await _botManager.ToggleMuteAsync();
    }

    public async Task ToggleBotDeafenAsync()
    {
        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected.", "red");
            return;
        }

        await _botManager.ToggleDeafenAsync();
    }
}
