using System.Reflection;
using Newtonsoft.Json;

namespace VoiceAttackDiscordPlugin.Config;

public class PluginConfig
{
    public string BotToken { get; set; } = "YOUR_BOT_TOKEN_HERE";
    public ulong DefaultGuildId { get; set; }
    public string DefaultChannelName { get; set; } = "general";
    public bool AutoConnect { get; set; } = true;
    public string LogLevel { get; set; } = "Info";

    private static string GetConfigPath()
    {
        // VoiceAttack's init proxy has no plugin-dir member; resolve from our own assembly location
        string? location = Assembly.GetExecutingAssembly().Location;
        string pluginDir = Path.GetDirectoryName(location) ?? AppContext.BaseDirectory;
        return Path.Combine(pluginDir, "config.json");
    }

    public static PluginConfig Load(dynamic va)
    {
        string path = GetConfigPath();

        if (!File.Exists(path))
        {
            var defaultConfig = new PluginConfig();
            Save(defaultConfig, va);
            va.WriteToLog("VoiceAttackDiscordPlugin: config.json created with default values. Please configure your bot token.", "yellow");
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<PluginConfig>(json);
            if (config == null)
            {
                va.WriteToLog("VoiceAttackDiscordPlugin: Failed to parse config.json, using defaults.", "red");
                return new PluginConfig();
            }
            return config;
        }
        catch (Exception ex)
        {
            va.WriteToLog($"VoiceAttackDiscordPlugin: Config load error: {ex.Message}", "red");
            return new PluginConfig();
        }
    }

    public static void Save(PluginConfig config, dynamic va)
    {
        string path = GetConfigPath();
        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}
