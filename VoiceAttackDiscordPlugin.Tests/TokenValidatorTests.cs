using VoiceAttackDiscordPlugin.Config;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class TokenValidatorTests
{
    [Theory]
    [InlineData("AAA.BBB.CCC")]
    [InlineData("abcDEF123-_-.xyz_0123456789.Aa")]
    [InlineData("  AAA.BBB.CCC  ")]
    public void PlausibleTokens_Accepted(string token)
    {
        Assert.True(TokenValidator.IsPlausible(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-token")]
    [InlineData("only.two")]
    [InlineData("too.many.dots.here")]
    [InlineData("has space.AAA.BBB")]
    [InlineData("YOUR_BOT_TOKEN_HERE")]
    public void ImplausibleTokens_Rejected(string? token)
    {
        Assert.False(TokenValidator.IsPlausible(token));
    }
}
