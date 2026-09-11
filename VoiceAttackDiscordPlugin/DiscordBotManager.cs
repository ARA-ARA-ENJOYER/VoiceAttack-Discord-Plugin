using Discord;
using Discord.Net;
using Discord.WebSocket;
using VoiceAttackDiscordPlugin.Config;

namespace VoiceAttackDiscordPlugin;

public class DiscordBotManager : IDisposable
{
    private readonly PluginConfig _config;
    private readonly dynamic _va;
    private DiscordSocketClient? _client;
    private TaskCompletionSource<bool> _readySource = new();
    private bool _disposed;

    // Serializes connect/disconnect so concurrent voice commands can't race _client.
    private readonly SemaphoreSlim _lifecycle = new(1, 1);

    // Short-lived per-guild user cache: user lookups otherwise sweep every guild
    // with paginated REST calls on each search (rate-limit pressure on big servers).
    private readonly object _cacheLock = new();
    private readonly Dictionary<ulong, (DateTimeOffset Fetched, IReadOnlyList<IGuildUser> Users)> _userCache = new();
    private static readonly TimeSpan UserCacheTtl = TimeSpan.FromMinutes(5);

    public DiscordSocketClient? Client => _client;
    public bool IsConnected => _client?.ConnectionState == ConnectionState.Connected;
    public Task Ready => _readySource.Task;
    public string DefaultChannelName => _config.DefaultChannelName ?? "general";

    public DiscordBotManager(PluginConfig config, dynamic va)
    {
        _config = config;
        _va = va;
    }

    public static LogSeverity ParseLogSeverity(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "verbose" or "debug" => LogSeverity.Debug,
        "warning" => LogSeverity.Warning,
        "error" => LogSeverity.Error,
        "critical" => LogSeverity.Critical,
        _ => LogSeverity.Info
    };

    /// <summary>
    /// Guilds to search. When DefaultGuildId is set, everything is scoped to that
    /// one guild (fewer REST calls, no cross-server ambiguity); otherwise all guilds.
    /// </summary>
    private IEnumerable<SocketGuild> TargetGuilds()
    {
        if (_client == null) return Enumerable.Empty<SocketGuild>();
        if (_config.DefaultGuildId != 0)
        {
            var scoped = _client.GetGuild(_config.DefaultGuildId);
            if (scoped == null)
                _va.WriteToLog($"Discord: DefaultGuildId {_config.DefaultGuildId} not found — searching all servers.", "yellow");
            else
                return new[] { scoped };
        }
        return _client.Guilds;
    }

    private async Task<IReadOnlyList<IGuildUser>> GetGuildUsersCachedAsync(SocketGuild guild)
    {
        lock (_cacheLock)
        {
            if (_userCache.TryGetValue(guild.Id, out var entry) && DateTimeOffset.UtcNow - entry.Fetched < UserCacheTtl)
                return entry.Users;
        }

        var users = (await guild.GetUsersAsync().FlattenAsync()).Cast<IGuildUser>().ToList();

        lock (_cacheLock)
        {
            _userCache[guild.Id] = (DateTimeOffset.UtcNow, users);
        }
        return users;
    }

    private void ClearUserCache()
    {
        lock (_cacheLock)
        {
            _userCache.Clear();
        }
    }

    public async Task ConnectAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_client != null)
            {
                if (IsConnected) return;
                await DisconnectCoreAsync();
            }

            _readySource = new TaskCompletionSource<bool>();

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.GuildMembers |
                                 GatewayIntents.GuildVoiceStates |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.DirectMessages,
                LogLevel = ParseLogSeverity(_config.LogLevel)
            };

            _client = new DiscordSocketClient(config);
            _client.Log += OnLog;
            _client.Ready += OnReady;
            _client.MessageReceived += OnMessageReceived;
            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;

            await _client.LoginAsync(TokenType.Bot, _config.ResolvedBotToken);
            await _client.StartAsync();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            await DisconnectCoreAsync();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task DisconnectCoreAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            await _client.DisposeAsync();
            _client = null;
        }
        ClearUserCache();
    }

    public void StopAll()
    {
        if (_client?.ConnectionState == ConnectionState.Connected)
        {
            _ = Task.Run(async () =>
            {
                try { await DisconnectAsync(); }
                catch (Exception ex) { _va.WriteToLog($"Discord: StopAll failed: {ex.Message}", "yellow"); }
            });
        }
    }

    public ISocketAudioChannel? GetCurrentVoiceChannel()
    {
        if (_client == null) return null;

        var selfUser = _client.CurrentUser;
        foreach (var guild in TargetGuilds())
        {
            var voiceState = guild.GetUser(selfUser.Id);
            if (voiceState?.VoiceChannel != null)
                return voiceState.VoiceChannel;
        }
        return null;
    }

    public async Task<ISocketAudioChannel?> JoinVoiceChannelAsync(string channelName)
    {
        if (_client == null) return null;

        foreach (var guild in TargetGuilds())
        {
            var channel = guild.VoiceChannels.FirstOrDefault(c =>
                string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));

            if (channel != null)
            {
                var audioClient = await channel.ConnectAsync();
                _va.WriteToLog($"Joined voice channel: {channel.Name}", "green");
                return channel;
            }
        }

        _va.WriteToLog($"Voice channel '{channelName}' not found.", "red");
        return null;
    }

    public async Task LeaveVoiceChannelAsync()
    {
        var currentChannel = GetCurrentVoiceChannel();
        if (currentChannel != null)
        {
            await currentChannel.DisconnectAsync();
            _va.WriteToLog($"Left voice channel: {currentChannel.Name}", "green");
        }
        else
        {
            _va.WriteToLog("Not currently in a voice channel.", "yellow");
        }
    }

    public async Task ToggleMuteAsync()
    {
        if (_client == null) return;

        var selfUser = _client.CurrentUser;
        foreach (var guild in TargetGuilds())
        {
            var voiceState = guild.GetUser(selfUser.Id);
            if (voiceState != null)
            {
                await voiceState.ModifyAsync(x => x.Mute = !voiceState.IsMuted);
                _va.WriteToLog($"Mute toggled: {!voiceState.IsMuted}", "green");
                return;
            }
        }
    }

    public async Task ToggleDeafenAsync()
    {
        if (_client == null) return;

        var selfUser = _client.CurrentUser;
        foreach (var guild in TargetGuilds())
        {
            var voiceState = guild.GetUser(selfUser.Id);
            if (voiceState != null)
            {
                await voiceState.ModifyAsync(x => x.Deaf = !voiceState.IsDeafened);
                _va.WriteToLog($"Deafen toggled: {!voiceState.IsDeafened}", "green");
                return;
            }
        }
    }

    public Task<ITextChannel?> FindTextChannelAsync(string channelName)
    {
        if (_client == null) return Task.FromResult<ITextChannel?>(null);

        foreach (var guild in TargetGuilds())
        {
            var channel = guild.TextChannels.FirstOrDefault(c =>
                string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));

            if (channel != null) return Task.FromResult<ITextChannel?>(channel);
        }
        return Task.FromResult<ITextChannel?>(null);
    }

    public async Task<IUser?> FindUserAsync(string userName)
    {
        if (_client == null) return null;

        foreach (var guild in TargetGuilds())
        {
            var users = await GetGuildUsersCachedAsync(guild);
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Username, userName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.GlobalName, userName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.DisplayName, userName, StringComparison.OrdinalIgnoreCase));

            if (user != null) return user;
        }
        return null;
    }

    // Username-only match for callbyusername: raw usernames (no @) are unique,
    // so a shared *display* name must never win over the real username here.
    public async Task<IUser?> FindUserByUsernameAsync(string userName)
    {
        if (_client == null) return null;

        foreach (var guild in TargetGuilds())
        {
            var users = await GetGuildUsersCachedAsync(guild);
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Username, userName, StringComparison.OrdinalIgnoreCase));

            if (user != null) return user;
        }
        return null;
    }

    public async Task<IUser?> FindUserByIdAsync(ulong userId)
    {
        if (_client == null) return null;

        // Fast path: cached user (ID lookups are name-change proof)
        var cached = _client.GetUser(userId);
        if (cached != null) return cached;

        // Fallback: REST fetch (works even if the user shares no cached guild)
        try
        {
            return await _client.Rest.GetUserAsync(userId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Counts distinct users (by ID) across the searched guilds sharing a display name.
    /// Used to detect Quick Switcher ambiguity: names are not unique, usernames are.
    /// </summary>
    public async Task<int> CountDistinctUsersByDisplayNameAsync(string displayName)
    {
        if (_client == null || string.IsNullOrWhiteSpace(displayName)) return 0;

        var ids = new HashSet<ulong>();
        foreach (var guild in TargetGuilds())
        {
            var users = await GetGuildUsersCachedAsync(guild);
            foreach (var u in users)
            {
                if (string.Equals(u.GlobalName ?? u.Username, displayName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(u.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    ids.Add(u.Id);
                }
            }
        }
        return ids.Count;
    }

    public Task<IReadOnlyCollection<IGuildUser>> GetChannelUsersAsync(string channelName)
    {
        if (_client == null)
            return Task.FromResult<IReadOnlyCollection<IGuildUser>>(Array.Empty<IGuildUser>());

        foreach (var guild in TargetGuilds())
        {
            var channel = guild.TextChannels.FirstOrDefault(c =>
                string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));

            if (channel != null)
            {
                // SocketTextChannel exposes cached guild users via Users (no GetUsersAsync in 3.x)
                IReadOnlyCollection<IGuildUser> users = channel.Users.Cast<IGuildUser>().ToList();
                return Task.FromResult(users);
            }
        }

        return Task.FromResult<IReadOnlyCollection<IGuildUser>>(Array.Empty<IGuildUser>());
    }

    public async Task<IMessageChannel?> FindDMChannelAsync(ulong userId)
    {
        if (_client == null) return null;

        try
        {
            var user = await _client.GetUserAsync(userId);
            if (user != null)
            {
                var dmChannel = await user.CreateDMChannelAsync();
                return dmChannel;
            }
        }
        catch { }
        return null;
    }

    private Task OnReady()
    {
        _readySource.TrySetResult(true);
        _va.WriteToLog($"Discord bot connected as {_client?.CurrentUser?.Username}", "green");
        return Task.CompletedTask;
    }

    private Task OnLog(LogMessage log)
    {
        var color = log.Severity switch
        {
            LogSeverity.Error => "red",
            LogSeverity.Warning => "yellow",
            _ => "blue"
        };
        _va.WriteToLog($"[Discord] {LogSanitizer.Sanitize(log.Message)}", color);
        return Task.CompletedTask;
    }

    private Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return Task.CompletedTask;
        _va.WriteToLog($"[Discord] {LogSanitizer.Sanitize(message.Author.Username, 64)}: {LogSanitizer.Sanitize(message.Content)}", "blue");
        return Task.CompletedTask;
    }

    private Task OnUserJoined(SocketGuildUser user)
    {
        _va.WriteToLog($"[Discord] {LogSanitizer.Sanitize(user.Username, 64)} joined {LogSanitizer.Sanitize(user.Guild.Name, 64)}", "blue");
        return Task.CompletedTask;
    }

    private Task OnUserLeft(SocketGuild guild, SocketUser user)
    {
        _va.WriteToLog($"[Discord] {LogSanitizer.Sanitize(user.Username, 64)} left {LogSanitizer.Sanitize(guild.Name, 64)}", "blue");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _lifecycle.Dispose();
            _disposed = true;
        }
    }
}
