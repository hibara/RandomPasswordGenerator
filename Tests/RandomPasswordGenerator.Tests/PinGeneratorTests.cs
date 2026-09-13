using RandomPasswordGenerator.Models;
using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator.Tests;

public class PinGeneratorTests
{
    private readonly PinGenerator _generator = new();

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(16)]
    public void 指定桁数の数字のみ(int length)
    {
        var generated = _generator.Generate(new PinOptions { Length = length });
        Assert.Equal(length, generated.Text.Length);
        Assert.All(generated.Text, c => Assert.True(char.IsAsciiDigit(c)));
        Assert.All(generated.Segments, s => Assert.Equal(CharacterKind.Digit, s.Kind));
    }

    [Fact]
    public void 強度は評価しない()
    {
        Assert.Null(_generator.Generate(new PinOptions { Length = 6 }).Strength);
    }

    [Fact]
    public void 範囲外の桁数は丸められる()
    {
        Assert.Equal(PinOptions.MinLength, _generator.Generate(new PinOptions { Length = 0 }).Text.Length);
        Assert.Equal(PinOptions.MaxLength, _generator.Generate(new PinOptions { Length = 100 }).Text.Length);
    }
}
