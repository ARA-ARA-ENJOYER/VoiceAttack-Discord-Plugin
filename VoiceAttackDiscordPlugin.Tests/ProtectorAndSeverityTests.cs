using System.Runtime.InteropServices;
using Discord;
using VoiceAttackDiscordPlugin.Config;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class ConfigTokenProtectorTests
{
    [Fact]
    public void ProtectUnprotect_RoundTrips()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // DPAPI is Windows-only

        const string secret = "AAA.BBB.CCC";
        string shielded = ConfigTokenProtector.Protect(secret);

        Assert.NotEqual(secret, shielded);
        Assert.Equal(secret, ConfigTokenProtector.Unprotect(shielded));
    }
}

public class LogSeverityParsingTests
{
    [Theory]
    [InlineData("debug", LogSeverity.Debug)]
    [InlineData("Verbose", LogSeverity.Debug)]
    [InlineData("warning", LogSeverity.Warning)]
    [InlineData("Error", LogSeverity.Error)]
    [InlineData("CRITICAL", LogSeverity.Critical)]
    [InlineData("info", LogSeverity.Info)]
    [InlineData("Info", LogSeverity.Info)]
    [InlineData("", LogSeverity.Info)]
    [InlineData(null, LogSeverity.Info)]
    [InlineData("nonsense", LogSeverity.Info)]
    public void ParseLogSeverity_MapsLevels(string? level, LogSeverity expected)
    {
        Assert.Equal(expected, DiscordBotManager.ParseLogSeverity(level));
    }
}
