using System.Runtime.InteropServices;
using VoiceAttackDiscordPlugin.Config;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class PluginConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vadiscord-tests", Guid.NewGuid().ToString("N"));

    public PluginConfigTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string PathFor(string name) => Path.Combine(_dir, name);

    [Fact]
    public void MissingFile_CreatesDefaults()
    {
        string path = PathFor("missing.json");

        var config = PluginConfig.LoadFrom(path, null);

        Assert.True(File.Exists(path));
        Assert.False(config.HasToken);
        Assert.Equal("", config.ResolvedBotToken);
    }

    [Fact]
    public void PlaintextToken_ResolvesOnEveryPlatform()
    {
        string path = PathFor("plain.json");
        PluginConfig.SaveTo(path, new PluginConfig { BotToken = "AAA.BBB.CCC" });

        var config = PluginConfig.LoadFrom(path, null);

        Assert.Equal("AAA.BBB.CCC", config.ResolvedBotToken);
        Assert.True(config.HasToken);
    }

    [Fact]
    public void PlaintextToken_MigratesToEncryptedOnWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        string path = PathFor("migrate.json");
        PluginConfig.SaveTo(path, new PluginConfig { BotToken = "AAA.BBB.CCC" });

        var config = PluginConfig.LoadFrom(path, null);

        Assert.Equal("AAA.BBB.CCC", config.ResolvedBotToken);
        Assert.NotEqual("", config.EncryptedBotToken);
        Assert.Equal("", config.BotToken); // plaintext no longer on disk

        // And the encrypted form reloads cleanly.
        var reloaded = PluginConfig.LoadFrom(path, null);
        Assert.Equal("AAA.BBB.CCC", reloaded.ResolvedBotToken);
    }

    [Fact]
    public void EncryptedToken_RoundTripsOnWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        string path = PathFor("enc.json");
        PluginConfig.SaveTo(path, new PluginConfig
        {
            BotToken = "",
            EncryptedBotToken = ConfigTokenProtector.Protect("AAA.BBB.CCC")
        });

        var config = PluginConfig.LoadFrom(path, null);

        Assert.Equal("AAA.BBB.CCC", config.ResolvedBotToken);
        Assert.True(config.HasToken);
    }

    [Fact]
    public void CorruptEncryptedToken_FailsClosed()
    {
        string path = PathFor("corrupt.json");
        PluginConfig.SaveTo(path, new PluginConfig
        {
            BotToken = "",
            EncryptedBotToken = "!!!not-base64!!!"
        });

        var config = PluginConfig.LoadFrom(path, null);

        Assert.Equal("", config.ResolvedBotToken);
        Assert.False(config.HasToken);
    }

    [Fact]
    public void ImplausibleToken_WarnsButDoesNotThrow()
    {
        string path = PathFor("implausible.json");
        PluginConfig.SaveTo(path, new PluginConfig { BotToken = "garbage" });

        var config = PluginConfig.LoadFrom(path, null);

        Assert.Equal("garbage", config.ResolvedBotToken);
        Assert.False(config.HasToken);
    }

    [Fact]
    public void MalformedJson_FallsBackToDefaults()
    {
        string path = PathFor("bad.json");
        File.WriteAllText(path, "{ this is not json");

        var config = PluginConfig.LoadFrom(path, null);

        Assert.False(config.HasToken);
    }
}
