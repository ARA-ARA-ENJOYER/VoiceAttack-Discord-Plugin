namespace DiscordVAPlugin.Commands;

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
        {
            _va.WriteToLog("Discord: No voice channel name provided for joinvoice.", "yellow");
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
            _va.SetVar("Discord.VoiceChannel", channel.Name);
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
        _va.SetVar("Discord.VoiceChannel", "");
    }

    public async Task ToggleMuteAsync()
    {
        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected.", "red");
            return;
        }

        await _botManager.ToggleMuteAsync();
    }

    public async Task ToggleDeafenAsync()
    {
        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected.", "red");
            return;
        }

        await _botManager.ToggleDeafenAsync();
    }
}
