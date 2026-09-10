using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceAttackDiscordPlugin.Config;

public class PluginConfig
{
    public const string PlaceholderToken = "YOUR_BOT_TOKEN_HERE";

    public string BotToken { get; set; } = PlaceholderToken;

    /// <summary>
    /// Base64 DPAPI (CurrentUser) protected token. Bound to this Windows user on
    /// this machine — a config.json copied elsewhere needs the token re-entered.
    /// </summary>
    public string EncryptedBotToken { get; set; } = "";
    public ulong DefaultGuildId { get; set; }
    public string DefaultChannelName { get; set; } = "general";
    public bool AutoConnect { get; set; } = true;
    public string LogLevel { get; set; } = "Info";

    /// <summary>Effective token after load (decrypted). Never serialized.</summary>
    [JsonIgnore]
    public string ResolvedBotToken { get; private set; } = "";

    [JsonIgnore]
    public bool HasToken => TokenValidator.IsPlausible(ResolvedBotToken);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string GetConfigPath()
    {
        // VoiceAttack's init proxy has no plugin-dir member; resolve from our own assembly location
        string? location = Assembly.GetExecutingAssembly().Location;
        string pluginDir = Path.GetDirectoryName(location) ?? AppContext.BaseDirectory;
        return Path.Combine(pluginDir, "config.json");
    }

    public static PluginConfig Load(dynamic? va) => LoadFrom(GetConfigPath(), va);

    public static PluginConfig LoadFrom(string path, dynamic? va)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = new PluginConfig();
            SaveTo(path, defaultConfig);
            va?.WriteToLog("VoiceAttackDiscordPlugin: config.json created with default values. Please configure your bot token.", "yellow");
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<PluginConfig>(json, JsonOptions);
            if (config == null)
            {
                va?.WriteToLog("VoiceAttackDiscordPlugin: Failed to parse config.json, using defaults.", "red");
                return new PluginConfig();
            }
            config.ResolveToken(path, va);
            return config;
        }
        catch (Exception ex)
        {
            va?.WriteToLog($"VoiceAttackDiscordPlugin: Config load error: {ex.Message}", "red");
            return new PluginConfig();
        }
    }

    private void ResolveToken(string path, dynamic? va)
    {
        ResolvedBotToken = "";

        string encrypted = (EncryptedBotToken ?? "").Trim();
        string plain = (BotToken ?? "").Trim();

        if (encrypted.Length > 0)
        {
            try
            {
                ResolvedBotToken = ConfigTokenProtector.Unprotect(encrypted).Trim();
            }
            catch (Exception ex)
            {
                va?.WriteToLog($"VoiceAttackDiscordPlugin: Could not decrypt the stored token ({ex.GetType().Name}). " +
                    "If this config.json was copied from another PC or user, re-enter your token via the setup wizard.", "red");
            }
            return;
        }

        if (plain.Length > 0 && plain != PlaceholderToken)
        {
            ResolvedBotToken = plain;
            if (!TokenValidator.IsPlausible(plain))
            {
                va?.WriteToLog("VoiceAttackDiscordPlugin: The stored token doesn't look like a bot token " +
                    "(three parts separated by dots). Re-enter it via the setup wizard.", "yellow");
                return;
            }

            // First run with a plaintext token: encrypt in place so it never sits on disk again.
            try
            {
                EncryptedBotToken = ConfigTokenProtector.Protect(plain);
                BotToken = "";
                SaveTo(path, this);
                va?.WriteToLog("VoiceAttackDiscordPlugin: Bot token encrypted for this Windows user (DPAPI).", "green");
            }
            catch (Exception ex)
            {
                va?.WriteToLog($"VoiceAttackDiscordPlugin: Token encryption unavailable ({ex.GetType().Name}); " +
                    "continuing with the plaintext token for this session.", "yellow");
            }
        }
    }

    public static void Save(PluginConfig config, dynamic? va) => SaveTo(GetConfigPath(), config);

    public static void SaveTo(string path, PluginConfig config)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }
}
