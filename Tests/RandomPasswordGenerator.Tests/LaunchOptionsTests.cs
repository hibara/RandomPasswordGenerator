using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator.Tests;

public class LaunchOptionsTests
{
    [Theory]
    [InlineData(new[] { "--lang=en" }, "en")]
    [InlineData(new[] { "--lang=ja" }, "ja")]
    [InlineData(new[] { "--lang", "en" }, "en")]
    [InlineData(new[] { "--LANG=JA" }, "ja")]
    [InlineData(new[] { "--lang=en-US" }, "en")]
    [InlineData(new[] { "--lang=ja_JP" }, "ja")]
    [InlineData(new[] { "--other", "--lang=ja", "x" }, "ja")]
    public void 対応言語を指定すると固定される(string[] args, string expected)
    {
        Assert.Equal(expected, LaunchOptions.Parse(args).UiCulture?.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--lang=fr")]
    [InlineData("--lang=")]
    [InlineData("--lang")]
    [InlineData("--language=en")]
    [InlineData("en")]
    public void 未指定や対応外の値はOSの言語に従う(string argsLine)
    {
        var args = argsLine.Length == 0 ? [] : argsLine.Split(' ');
        Assert.Null(LaunchOptions.Parse(args).UiCulture);
    }

    [Fact]
    public void 複数あれば最後の指定が有効()
    {
        Assert.Equal("en", LaunchOptions.Parse(["--lang=ja", "--lang=en"]).UiCulture?.Name);
    }
}
