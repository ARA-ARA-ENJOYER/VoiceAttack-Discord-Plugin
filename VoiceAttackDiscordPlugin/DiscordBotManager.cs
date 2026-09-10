using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordVAPlugin.Config;

namespace DiscordVAPlugin;

public class DiscordBotManager : IDisposable
{
    private readonly PluginConfig _config;
    private readonly dynamic _va;
    private DiscordSocketClient? _client;
    private readonly TaskCompletionSource<bool> _readySource = new();
    private bool _disposed;

    public DiscordSocketClient? Client => _client;
    public bool IsConnected => _client?.ConnectionState == ConnectionState.Connected;
    public Task Ready => _readySource.Task;

    public DiscordBotManager(PluginConfig config, dynamic va)
    {
        _config = config;
        _va = va;
    }

    public async Task ConnectAsync()
    {
        if (_client != null)
        {
            if (IsConnected) return;
            await DisconnectAsync();
        }

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMessages |
                             GatewayIntents.GuildMembers |
                             GatewayIntents.GuildVoiceStates |
                             GatewayIntents.MessageContent |
                             GatewayIntents.DirectMessages,
            LogLevel = LogSeverity.Info
        };

        _client = new DiscordSocketClient(config);
        _client.Log += OnLog;
        _client.Ready += OnReady;
        _client.MessageReceived += OnMessageReceived;
        _client.UserJoined += OnUserJoined;
        _client.UserLeft += OnUserLeft;

        await _client.LoginAsync(TokenType.Bot, _config.BotToken);
        await _client.StartAsync();
    }

    public async Task DisconnectAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            await _client.DisposeAsync();
            _client = null;
        }
    }

    public void StopAll()
    {
        if (_client?.ConnectionState == ConnectionState.Connected)
        {
            _ = Task.Run(async () => await DisconnectAsync());
        }
    }

    public ISocketAudioChannel? GetCurrentVoiceChannel()
    {
        if (_client?.Guilds == null) return null;

        var selfUser = _client.CurrentUser;
        foreach (var guild in _client.Guilds)
        {
            var voiceState = guild.GetUser(selfUser.Id);
            if (voiceState?.VoiceChannel != null)
                return voiceState.VoiceChannel;
        }
        return null;
    }

    public async Task<ISocketAudioChannel?> JoinVoiceChannelAsync(string channelName)
    {
        if (_client?.Guilds == null) return null;

        foreach (var guild in _client.Guilds)
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
        if (_client?.Guilds == null) return;

        var selfUser = _client.CurrentUser;
        foreach (var guild in _client.Guilds)
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
        if (_client?.Guilds == null) return;

        var selfUser = _client.CurrentUser;
        foreach (var guild in _client.Guilds)
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

    public async Task<ITextChannel?> FindTextChannelAsync(string channelName)
    {
        if (_client?.Guilds == null) return null;

        foreach (var guild in _client.Guilds)
        {
            var channel = guild.TextChannels.FirstOrDefault(c =>
                string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));

            if (channel != null) return channel;
        }
        return null;
    }

    public async Task<IUser?> FindUserAsync(string userName)
    {
        if (_client?.Guilds == null) return null;

        foreach (var guild in _client.Guilds)
        {
            var users = await guild.GetUsersAsync().FlattenAsync();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Username, userName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.GlobalName, userName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.DisplayName, userName, StringComparison.OrdinalIgnoreCase));

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
    /// Counts distinct users (by ID) across all guilds sharing a display name.
    /// Used to detect Quick Switcher ambiguity: names are not unique, usernames are.
    /// </summary>
    public async Task<int> CountDistinctUsersByDisplayNameAsync(string displayName)
    {
        if (_client?.Guilds == null || string.IsNullOrWhiteSpace(displayName)) return 0;

        var ids = new HashSet<ulong>();
        foreach (var guild in _client.Guilds)
        {
            var users = await guild.GetUsersAsync().FlattenAsync();
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

    public async Task<IReadOnlyCollection<IGuildUser>> GetChannelUsersAsync(string channelName)
    {
        if (_client?.Guilds == null)
            return Array.Empty<IGuildUser>();

        foreach (var guild in _client.Guilds)
        {
            var channel = guild.TextChannels.FirstOrDefault(c =>
                string.Equals(c.Name, channelName, StringComparison.OrdinalIgnoreCase));

            if (channel != null)
            {
                // SocketTextChannel exposes cached guild users via Users (no GetUsersAsync in 3.x)
                return channel.Users.Cast<IGuildUser>().ToList();
            }
        }

        return Array.Empty<IGuildUser>();
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
        _va.WriteToLog($"[Discord] {log.Message}", color);
        return Task.CompletedTask;
    }

    private Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return Task.CompletedTask;
        _va.WriteToLog($"[Discord] {message.Author.Username}: {message.Content}", "blue");
        return Task.CompletedTask;
    }

    private Task OnUserJoined(SocketGuildUser user)
    {
        _va.WriteToLog($"[Discord] {user.Username} joined {user.Guild.Name}", "blue");
        return Task.CompletedTask;
    }

    private Task OnUserLeft(SocketGuild guild, SocketUser user)
    {
        _va.WriteToLog($"[Discord] {user.Username} left {guild.Name}", "blue");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _disposed = true;
        }
    }
}
