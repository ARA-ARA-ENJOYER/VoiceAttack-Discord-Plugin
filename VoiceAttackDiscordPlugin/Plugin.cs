using VoiceAttackDiscordPlugin.Config;

namespace VoiceAttackDiscordPlugin;

public class Plugin
{
    private static readonly Guid PluginId = new("{A7B8C9D0-E1F2-3456-7890-ABCDEF123456}");
    private const string PluginName = "VoiceAttackDiscordPlugin";
    private const string PluginVersion = "1.0.0";

    private static dynamic? _va;
    private static DiscordBotManager? _botManager;
    private static CommandRouter? _router;
    private static PluginConfig? _config;

    public static Guid VA_Id() => PluginId;

    public static string VA_DisplayName() => "VoiceAttack Discord Plugin";

    public static string VA_DisplayInfo()
    {
        return $"{PluginName} v{PluginVersion}\r\n" +
               $"Discord integration for VoiceAttack\r\n" +
               $"Features: messaging, voice channels, user search, call initiation\r\n" +
               $"Requires a Discord Bot token configured in config.json";
    }

    public static void VA_Init1(dynamic vaProxy)
    {
        _va = vaProxy;

        try
        {
            _config = PluginConfig.Load(vaProxy);
            _botManager = new DiscordBotManager(_config, vaProxy);
            _router = new CommandRouter(_botManager, vaProxy);

            vaProxy.WriteToLog($"{PluginName} initialized. Bot token configured: {(!string.IsNullOrEmpty(_config.BotToken) && _config.BotToken != "YOUR_BOT_TOKEN_HERE")}", "green");

            if (_config.AutoConnect && !string.IsNullOrEmpty(_config.BotToken) && _config.BotToken != "YOUR_BOT_TOKEN_HERE")
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _botManager.ConnectAsync();
                        vaProxy.WriteToLog($"{PluginName} connected to Discord automatically.", "green");
                    }
                    catch (Exception ex)
                    {
                        vaProxy.WriteToLog($"{PluginName} auto-connect failed: {ex.Message}", "red");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            vaProxy.WriteToLog($"{PluginName} initialization error: {ex.Message}", "red");
        }
    }

    public static void VA_Exit1(dynamic vaProxy)
    {
        try
        {
            _ = Task.Run(async () =>
            {
                if (_botManager != null)
                {
                    await _botManager.DisconnectAsync();
                }
            });

            vaProxy.WriteToLog($"{PluginName} shutting down.", "yellow");
        }
        catch (Exception ex)
        {
            vaProxy.WriteToLog($"{PluginName} shutdown error: {ex.Message}", "red");
        }
    }

    public static void VA_StopCommand()
    {
        try
        {
            _botManager?.StopAll();
        }
        catch { }
    }

    public static void VA_Invoke1(dynamic vaProxy)
    {
        try
        {
            if (_router == null || _botManager == null)
            {
                vaProxy.WriteToLog($"{PluginName} not initialized. Please restart VoiceAttack.", "red");
                return;
            }

            // V4 interface: only Context is passed directly (Text1/2/3 do not exist).
            // Convention: Context carries "action:arg1:arg2" (colon-delimited).
            // Context parses {TXT:...} tokens, so dictation/variables can be embedded.
            string fullContext = vaProxy.Context?.ToString() ?? "";
            var parts = fullContext.Split(new[] { ':' }, StringSplitOptions.None);

            string context = parts.Length > 0 ? parts[0].Trim() : "";
            string text1 = parts.Length > 1 ? parts[1].Trim() : "";
            string text2 = parts.Length > 2 ? parts[2].Trim() : "";
            string text3 = parts.Length > 3 ? string.Join(":", parts.Skip(3)).Trim() : "";

            _ = Task.Run(async () =>
            {
                try
                {
                    await _router.RouteAsync(context, text1, text2, text3);
                }
                catch (Exception ex)
                {
                    vaProxy.WriteToLog($"{PluginName} command error ({context}): {ex.Message}", "red");
                }
            });
        }
        catch (Exception ex)
        {
            vaProxy.WriteToLog($"{PluginName} invoke error: {ex.Message}", "red");
        }
    }
}
