using ClaudeExplorer.App.Environments;

namespace ClaudeExplorer.App.Tests.Environments;

public class WslLocatorSanitizeTests
{
    [Fact]
    public void CleanLines_strips_utf16_nul_bytes_and_blanks()
    {
        // "Ubuntu\nDebian" as UTF-16LE captured as bytes → interleaved NULs + a trailing blank.
        var raw = "U\0b\0u\0n\0t\0u\0\r\0\n\0D\0e\0b\0i\0a\0n\0\r\0\n\0\r\0\n\0";

        var lines = WslLocator.CleanLines(raw);

        Assert.Equal(new[] { "Ubuntu", "Debian" }, lines);
    }

    [Fact]
    public void CleanLines_handles_plain_utf8_too()
    {
        Assert.Equal(new[] { "Ubuntu" }, WslLocator.CleanLines("Ubuntu\r\n"));
    }

    [Fact]
    public void CleanPath_trims_nul_cr_and_whitespace()
    {
        Assert.Equal(@"\\wsl.localhost\Ubuntu\home\p",
            WslLocator.CleanPath("\\\\wsl.localhost\\Ubuntu\\home\\p\r\n"));
        Assert.Null(WslLocator.CleanPath("   \r\n"));
        Assert.Null(WslLocator.CleanPath(null));
    }
}
