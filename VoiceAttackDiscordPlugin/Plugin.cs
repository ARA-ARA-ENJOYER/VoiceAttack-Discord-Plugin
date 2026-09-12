using System.Reflection;
using VoiceAttackDiscordPlugin.Config;

namespace VoiceAttackDiscordPlugin;

public class Plugin
{
    private static readonly Guid PluginId = new("{A7B8C9D0-E1F2-3456-7890-ABCDEF123456}");
    private const string PluginName = "VoiceAttackDiscordPlugin";

    // Single source of truth: <Version> in the .csproj
    private static string PluginVersion =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    private static dynamic? _va;
    private static DiscordBotManager? _botManager;
    private static CommandRouter? _router;
    private static PluginConfig? _config;

    // Tracked background work so shutdown can wait for it briefly instead of abandoning it.
    private static readonly object _tasksLock = new();
    private static readonly List<Task> _inflight = new();

    private static void Track(Task task)
    {
        lock (_tasksLock)
        {
            _inflight.RemoveAll(t => t.IsCompleted);
            _inflight.Add(task);
        }
    }

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

            vaProxy.WriteToLog($"{PluginName} initialized. Bot token configured: {_config.HasToken}", "green");

            // Update check: release tag vs. our own version. Fire-and-forget,
            // tracked so shutdown waits for it briefly; never blocks commands.
            Track(Task.Run(async () =>
            {
                try
                {
                    if (GitHubReleaseChecker.LatestVersionFromTag(PluginVersion) == null) return; // unknown build — stay quiet
                    var latest = await GitHubReleaseChecker.FetchLatestVersionAsync();
                    var compare = GitHubReleaseChecker.CompareVersions(PluginVersion, latest);
                    if (compare < 0)
                        vaProxy.WriteToLog($"{PluginName}: Update available: v{latest} (you have v{PluginVersion}). Download: {GitHubReleaseChecker.ReleasesUrl}", "green");
                    else if (latest != null)
                        vaProxy.WriteToLog($"{PluginName}: You're up to date (v{PluginVersion}).", "green");
                    // compare > 0: dev build newer than the release — stay quiet.
                }
                catch
                {
                    vaProxy.WriteToLog($"{PluginName}: Couldn't check for updates (offline?). Continuing with v{PluginVersion}.", "yellow");
                }
            }));

            if (!_config.HasToken)
            {
                vaProxy.WriteToLog($"{PluginName}: No bot token yet. Run the setup wizard " +
                    "(or paste the token into config.json — it encrypts itself on next load).", "yellow");
            }
            else if (_config.AutoConnect)
            {
                Track(Task.Run(async () =>
                {
                    try
                    {
                        await _botManager.ConnectAsync();
                        try
                        {
                            await _botManager.Ready.WaitAsync(TimeSpan.FromSeconds(15));
                            vaProxy.WriteToLog($"{PluginName} connected to Discord automatically.", "green");
                        }
                        catch (TimeoutException)
                        {
                            vaProxy.WriteToLog($"{PluginName} connected, but Discord is still " +
                                "handshaking (READY timed out). Commands may fail for a few seconds.", "yellow");
                        }
                    }
                    catch (Exception ex)
                    {
                        vaProxy.WriteToLog($"{PluginName} auto-connect failed: {ex.Message}", "red");
                    }
                }));
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
            List<Task> pending;
            lock (_tasksLock)
            {
                pending = _inflight.Where(t => !t.IsCompleted).ToList();
                _inflight.Clear();
            }
            if (pending.Count > 0)
            {
                try { Task.WhenAll(pending).Wait(TimeSpan.FromSeconds(3)); }
                catch { /* shutting down; never block VoiceAttack */ }
            }

            if (_botManager != null)
            {
                try { _botManager.DisconnectAsync().Wait(TimeSpan.FromSeconds(5)); }
                catch (Exception ex)
                {
                    vaProxy.WriteToLog($"{PluginName} shutdown disconnect: {ex.Message}", "yellow");
                }
                try { _botManager.Dispose(); }
                catch { /* shutting down; never block VoiceAttack */ }
                _botManager = null;
                _router = null;
            }

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
        catch (Exception ex)
        {
            try { _va?.WriteToLog($"{PluginName} stop error: {ex.Message}", "yellow"); } catch { }
        }
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
            var parsed = CommandContext.Parse(vaProxy.Context?.ToString());
            string context = parsed.Action;
            string text1 = parsed.Arg1;
            string text2 = parsed.Arg2;
            string text3 = parsed.Arg3;

            Track(Task.Run(async () =>
            {
                try
                {
                    await _router.RouteAsync(context, text1, text2, text3);
                }
                catch (Exception ex)
                {
                    vaProxy.WriteToLog($"{PluginName} command error ({context}): {ex.Message}", "red");
                }
            }));
        }
        catch (Exception ex)
        {
            vaProxy.WriteToLog($"{PluginName} invoke error: {ex.Message}", "red");
        }
    }
}
