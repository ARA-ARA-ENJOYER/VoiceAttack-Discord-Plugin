using VoiceAttackDiscordPlugin.Commands;

namespace VoiceAttackDiscordPlugin;

public class CommandRouter
{
    private readonly DiscordBotManager _botManager;
    private readonly dynamic _va;
    private readonly MessagingCommands _messaging;
    private readonly VoiceCommands _voice;
    private readonly UserCommands _users;
    private readonly CallCommands _call;

    public CommandRouter(DiscordBotManager botManager, dynamic va)
    {
        _botManager = botManager;
        _va = va;
        _messaging = new MessagingCommands(botManager, va);
        _voice = new VoiceCommands(botManager, va);
        _users = new UserCommands(botManager, va);
        _call = new CallCommands(botManager, va);
    }

    public async Task RouteAsync(string context, string text1, string text2, string text3)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            _va.WriteToLog("VoiceAttackDiscordPlugin: No context provided.", "yellow");
            return;
        }

        var ctx = context.ToLower().Trim();

        switch (ctx)
        {
            case "connect":
                await HandleConnect();
                break;
            case "disconnect":
                await HandleDisconnect();
                break;
            case "sendmessage":
                await _messaging.SendMessageAsync(text1, text2);
                break;
            case "readmessages":
                await _messaging.ReadMessagesAsync(text1, text2);
                break;
            case "senddm":
                await _messaging.SendDMAsync(text1, text2);
                break;
            case "searchuser":
                await _users.SearchUserAsync(text1);
                break;
            case "searchuserid":
                await _users.SearchUserIdAsync(text1);
                break;
            case "listusers":
                await _users.ListUsersAsync(text1);
                break;
            case "joinvoice":
                await _voice.JoinVoiceAsync(text1);
                break;
            case "leavevoice":
                await _voice.LeaveVoiceAsync();
                break;
            case "mute":
                await _voice.ToggleMuteAsync();
                break;
            case "deafen":
                await _voice.ToggleDeafenAsync();
                break;
            case "calluser":
                await _call.CallUserAsync(text1);
                break;
            case "callbyid":
                await _call.CallUserByIdAsync(text1);
                break;
            case "callbyusername":
                await _call.CallByUsernameAsync(text1);
                break;
            default:
                _va.WriteToLog($"VoiceAttackDiscordPlugin: Unknown context '{context}'.", "yellow");
                break;
        }
    }

    private async Task HandleConnect()
    {
        if (_botManager.IsConnected)
        {
            _va.WriteToLog("VoiceAttackDiscordPlugin: Already connected.", "yellow");
            return;
        }

        try
        {
            await _botManager.ConnectAsync();
            _va.WriteToLog("VoiceAttackDiscordPlugin: Connected to Discord.", "green");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"VoiceAttackDiscordPlugin: Connection failed: {ex.Message}", "red");
        }
    }

    private async Task HandleDisconnect()
    {
        if (!_botManager.IsConnected)
        {
            _va.WriteToLog("VoiceAttackDiscordPlugin: Not connected.", "yellow");
            return;
        }

        try
        {
            await _botManager.DisconnectAsync();
            _va.WriteToLog("VoiceAttackDiscordPlugin: Disconnected from Discord.", "green");
        }
        catch (Exception ex)
        {
            _va.WriteToLog($"VoiceAttackDiscordPlugin: Disconnect failed: {ex.Message}", "red");
        }
    }
}
