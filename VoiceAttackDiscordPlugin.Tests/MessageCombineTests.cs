using VoiceAttackDiscordPlugin;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class MessageCombineTests
{
    [Fact]
    public void Combine_NoTail_ReturnsMessageUnchanged()
    {
        Assert.Equal("hello world", CommandRouter.Combine("hello world", ""));
    }

    [Fact]
    public void Combine_UrlTail_RestoresFullMessage()
    {
        var ctx = CommandContext.Parse("sendmessage:general:the link is https://x.com");
        Assert.Equal("general", ctx.Arg1);
        Assert.Equal("the link is https", ctx.Arg2);
        Assert.Equal("//x.com", ctx.Arg3);
        Assert.Equal("the link is https://x.com",
            CommandRouter.Combine(ctx.Arg2, ctx.Arg3));
    }

    [Fact]
    public void Combine_MultipleColons_RoundTripsExactly()
    {
        const string original = "meet at 12:30, see https://x.com:8080/a?b=c";
        var ctx = CommandContext.Parse($"sendmessage:general:{original}");
        Assert.Equal(original, CommandRouter.Combine(ctx.Arg2, ctx.Arg3));
    }

    [Fact]
    public void Combine_EmptyChannelForm_KeepsMessage()
    {
        var ctx = CommandContext.Parse("sendmessage::hello world");
        Assert.Equal("", ctx.Arg1);
        Assert.Equal("hello world", CommandRouter.Combine(ctx.Arg2, ctx.Arg3));
    }
}
