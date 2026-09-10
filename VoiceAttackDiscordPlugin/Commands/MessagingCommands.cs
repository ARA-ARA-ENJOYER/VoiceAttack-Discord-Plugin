using Discord;

namespace VoiceAttackDiscordPlugin.Commands;

public class MessagingCommands
{
    private readonly DiscordBotManager _botManager;
    private readonly dynamic _va;

    public MessagingCommands(DiscordBotManager botManager, dynamic va)
    {
        _botManager = botManager;
        _va = va;
    }

    public async Task SendMessageAsync(string channelName, string message)
    {
        // Empty channel falls back to the configured default (lets `sendmessage:<message>` work
        // when the Context only carries the text — see CommandContext parsing).
        if (string.IsNullOrWhiteSpace(channelName))
            channelName = _botManager.DefaultChannelName;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            _va.WriteToLog("Discord: No channel name provided for sendmessage (and no DefaultChannelName configured).", "yellow");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _va.WriteToLog("Discord: No message provided for sendmessage.", "yellow");
            return;
        }

        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        var channel = await _botManager.FindTextChannelAsync(channelName);
        if (channel == null)
        {
            _va.WriteToLog($"Discord: Channel '{channelName}' not found.", "red");
            return;
        }

        try
        {
            await channel.SendMessageAsync(message);
            _va.WriteToLog($"Discord: Message sent to #{channelName}.", "green");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Failed to send message: {ex.Message}", "red");
        }
    }

    public async Task ReadMessagesAsync(string channelName, string countStr)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            channelName = _botManager.DefaultChannelName;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            _va.WriteToLog("Discord: No channel name provided for readmessages (and no DefaultChannelName configured).", "yellow");
            return;
        }

        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("Discord: Not connected. Use 'connect' context first.", "red");
            return;
        }

        int count = 10;
        if (!string.IsNullOrWhiteSpace(countStr) && int.TryParse(countStr, out int parsed))
        {
            count = Math.Clamp(parsed, 1, 100);
        }

        var channel = await _botManager.FindTextChannelAsync(channelName);
        if (channel == null)
        {
            _va.WriteToLog($"Discord: Channel '{channelName}' not found.", "red");
            return;
        }

        try
        {
            var messages = await channel.GetMessagesAsync(count).FlattenAsync();
            var messageList = messages.Reverse().ToList();

            // Sanitize: channel content can contain control characters; keep the
            // log line and the TTS variable single-line and bounded.
            var result = string.Join("\n", messageList.Select(m =>
                $"[{m.CreatedAt:HH:mm}] {LogSanitizer.Sanitize(m.Author.Username, 64)}: {LogSanitizer.Sanitize(m.Content, 2000)}"));
            if (result.Length > 8000)
                result = result.Substring(0, 8000) + "… [truncated]";

            _va.SetText("Discord.LastMessages", result);
            _va.SetText("Discord.LastMessageCount", messageList.Count.ToString());
            _va.WriteToLog($"Discord: Read {messageList.Count} messages from #{channelName}.", "green");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Failed to read messages: {ex.Message}", "red");
        }
    }

    public async Task SendDMAsync(string userName, string message)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            _va.WriteToLog("Discord: No user name provided for senddm.", "yellow");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _va.WriteToLog("Discord: No message provided for senddm.", "yellow");
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
            return;
        }

        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            await dmChannel.SendMessageAsync(message);
            _va.WriteToLog($"Discord: DM sent to {userName}.", "green");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"Discord: Failed to send DM: {ex.Message}", "red");
        }
    }
}
