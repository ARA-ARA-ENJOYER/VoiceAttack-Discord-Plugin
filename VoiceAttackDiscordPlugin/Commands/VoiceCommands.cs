namespace VoiceAttackDiscordPlugin.Commands;

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
