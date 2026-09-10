using VoiceAttackDiscordPlugin.Commands;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class CallSearchTextTests
{
    [Fact]
    public void UniqueName_UsesDisplayName()
    {
        Assert.Equal("Some Name", CallSearchText.Select("Some Name", 1, "someuser"));
    }

    [Fact]
    public void SharedName_FallsBackToAtUsername()
    {
        Assert.Equal("@someuser", CallSearchText.Select("Some Name", 3, "someuser"));
    }

    [Fact]
    public void ZeroMatches_UsesDisplayName()
    {
        Assert.Equal("Some Name", CallSearchText.Select("Some Name", 0, "someuser"));
    }
}
