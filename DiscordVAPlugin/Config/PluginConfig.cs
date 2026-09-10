using Newtonsoft.Json;

namespace DiscordVAPlugin.Config;

public class PluginConfig
{
    public string BotToken { get; set; } = "YOUR_BOT_TOKEN_HERE";
    public ulong DefaultGuildId { get; set; }
    public string DefaultChannelName { get; set; } = "general";
    public bool AutoConnect { get; set; } = true;
    public string LogLevel { get; set; } = "Info";

    private static string GetConfigPath(dynamic va)
    {
        string pluginDir = Path.GetDirectoryName(va.PluginDir?.ToString() ?? "") ?? "";
        return Path.Combine(pluginDir, "config.json");
    }

    public static PluginConfig Load(dynamic va)
    {
        string path = GetConfigPath(va);

        if (!File.Exists(path))
        {
            var defaultConfig = new PluginConfig();
            Save(defaultConfig, va);
            va.WriteToLog("DiscordVAPlugin: config.json created with default values. Please configure your bot token.", "yellow");
            return defaultConfig;
        }

        try
        {
            string json = File.ReadAllText(path);
            var config = JsonConvert.DeserializeObject<PluginConfig>(json);
            if (config == null)
            {
                va.WriteToLog("DiscordVAPlugin: Failed to parse config.json, using defaults.", "red");
                return new PluginConfig();
            }
            return config;
        }
        catch (Exception ex)
        {
            va.WriteToLog($"DiscordVAPlugin: Config load error: {ex.Message}", "red");
            return new PluginConfig();
        }
    }

    public static void Save(PluginConfig config, dynamic va)
    {
        string path = GetConfigPath(va);
        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}
