using VoiceAttackDiscordPlugin;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class CommandContextTests
{
    [Fact]
    public void Parse_ActionOnly()
    {
        var ctx = CommandContext.Parse("connect");
        Assert.Equal("connect", ctx.Action);
        Assert.Equal("", ctx.Arg1);
        Assert.Equal("", ctx.Arg2);
        Assert.Equal("", ctx.Arg3);
    }

    [Fact]
    public void Parse_TwoArgs()
    {
        var ctx = CommandContext.Parse("sendmessage:general:hello world");
        Assert.Equal("sendmessage", ctx.Action);
        Assert.Equal("general", ctx.Arg1);
        Assert.Equal("hello world", ctx.Arg2);
        Assert.Equal("", ctx.Arg3);
    }

    [Fact]
    public void Parse_ExtraColonsStayInArg3()
    {
        var ctx = CommandContext.Parse("readmessages:general:10:extra:bits");
        Assert.Equal("readmessages", ctx.Action);
        Assert.Equal("general", ctx.Arg1);
        Assert.Equal("10", ctx.Arg2);
        Assert.Equal("extra:bits", ctx.Arg3);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var ctx = CommandContext.Parse("  callbyid :  123456789012345678  ");
        Assert.Equal("callbyid", ctx.Action);
        Assert.Equal("123456789012345678", ctx.Arg1);
    }

    [Fact]
    public void Parse_NullAndEmpty()
    {
        var fromNull = CommandContext.Parse(null);
        Assert.Equal("", fromNull.Action);
        Assert.Equal("", fromNull.Arg1);

        var fromEmpty = CommandContext.Parse("");
        Assert.Equal("", fromEmpty.Action);
    }
}
