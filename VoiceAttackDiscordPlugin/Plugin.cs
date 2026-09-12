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

    // User-facing version without the git-SHA suffix baked into InformationalVersion.
    private static string DisplayVersion =>
        GitHubReleaseChecker.LatestVersionFromTag(PluginVersion) ?? PluginVersion;

    private static CancellationTokenSource? _updateCts;

    // Folders the plugin used to live in (pre-1.4 renames). If one sits next
    // to the current folder after an upgrade, VoiceAttack loads the plugin twice.
    private static readonly string[] LegacyFolderNames = { "VA.VoiceAttackDiscordPlugin", "VA.DiscordVAPlugin" };

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
        return $"{PluginName} v{DisplayVersion}\r\n" +
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

            WarnIfLegacyFoldersPresent(vaProxy);

            // Update check: release tag vs. our own version. Fire-and-forget,
            // tracked so shutdown waits for it briefly; never blocks commands.
            _updateCts?.Dispose();
            _updateCts = new CancellationTokenSource();
            var updateCt = _updateCts.Token;
            Track(Task.Run(async () =>
            {
                try
                {
                    var latest = await GitHubReleaseChecker.FetchLatestVersionAsync(updateCt);
                    var message = GitHubReleaseChecker.BuildUpdateMessage(PluginVersion, latest);
                    if (message != null)
                        vaProxy.WriteToLog($"{PluginName}: {message}", "green");
                    // null: unknown versions or dev build — stay quiet.
                }
                catch (OperationCanceledException)
                {
                    // Shutting down — stay quiet.
                }
                catch
                {
                    vaProxy.WriteToLog($"{PluginName}: Couldn't check for updates (offline?). Continuing with v{DisplayVersion}.", "yellow");
                }
            }, updateCt));

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

    // Courtesy warning for manual upgraders: a leftover legacy folder next to
    // the current one makes VoiceAttack load the plugin twice.
    private static void WarnIfLegacyFoldersPresent(dynamic vaProxy)
    {
        try
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var apps = dir != null ? Directory.GetParent(dir)?.FullName : null;
            if (apps == null) return;
            foreach (var legacy in LegacyFolderNames)
            {
                var legacyDir = Path.Combine(apps, legacy);
                if (string.Equals(dir, legacyDir, StringComparison.OrdinalIgnoreCase))
                    continue; // that's us (a non-renamed install) — nothing to warn about
                if (Directory.Exists(legacyDir))
                    vaProxy.WriteToLog($"{PluginName}: Legacy plugin folder '{legacy}' detected next to the current one — remove it to avoid loading the plugin twice.", "yellow");
            }
        }
        catch
        {
            // Never break init over a courtesy warning.
        }
    }

    public static void VA_Exit1(dynamic vaProxy)
    {
        try
        {
            try { _updateCts?.Cancel(); }
            catch { /* shutting down; never block VoiceAttack */ }
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
