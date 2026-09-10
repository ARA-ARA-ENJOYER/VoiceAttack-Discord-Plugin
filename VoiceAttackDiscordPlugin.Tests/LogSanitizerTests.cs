using VoiceAttackDiscordPlugin;
using Xunit;

namespace VoiceAttackDiscordPlugin.Tests;

public class LogSanitizerTests
{
    [Fact]
    public void NullAndEmpty_ReturnEmpty()
    {
        Assert.Equal("", LogSanitizer.Sanitize(null));
        Assert.Equal("", LogSanitizer.Sanitize(""));
    }

    [Fact]
    public void ControlCharacters_ReplacedWithSpaces()
    {
        string clean = LogSanitizer.Sanitize("line1\r\nline2\tcol");
        Assert.DoesNotContain("\r", clean);
        Assert.DoesNotContain("\n", clean);
        Assert.DoesNotContain("\t", clean);
        Assert.Contains("line1", clean);
        Assert.Contains("line2", clean);
    }

    [Fact]
    public void LongText_TruncatedWithEllipsis()
    {
        string clean = LogSanitizer.Sanitize(new string('x', 500), 200);
        Assert.Equal(201, clean.Length); // 200 chars + …
        Assert.EndsWith("…", clean);
    }

    [Fact]
    public void ShortText_Untouched()
    {
        Assert.Equal("hello", LogSanitizer.Sanitize("  hello  "));
    }
}
