using PasswordStrength;
using RandomPasswordGenerator.Models;
using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator.Tests;

public class PasswordGeneratorTests
{
    private static readonly PasswordStrengthService Strength = new();
    private readonly PasswordGenerator _generator = new(Strength);

    [Fact]
    public void 四種すべてで紛らわしい文字を省かない場合の候補は94文字()
    {
        var pool = PasswordGenerator.BuildPool(new PasswordOptions
        {
            IncludeUppercase = true, IncludeLowercase = true, IncludeDigits = true, IncludeSymbols = true, OmitConfusing = false,
        });
        Assert.Equal(26 + 26 + 10 + 32, pool.Length);
        Assert.Equal(pool.Length, pool.Distinct().Count());
    }

    [Fact]
    public void 英大小文字と数字なら候補は62文字()
    {
        var pool = PasswordGenerator.BuildPool(new PasswordOptions
        {
            IncludeUppercase = true, IncludeLowercase = true, IncludeDigits = true, IncludeSymbols = false,
        });
        Assert.Equal(62, pool.Length);
    }

    [Fact]
    public void 生成アルゴリズムと強度計算が一致する_62文字から20文字()
    {
        var options = new PasswordOptions { Length = 20, IncludeUppercase = true, IncludeLowercase = true, IncludeDigits = true, IncludeSymbols = false };
        var generated = _generator.Generate(options);

        Assert.Equal(20, generated.Text.Length);
        Assert.NotNull(generated.Strength);
        Assert.Equal(PasswordStrengthSource.GeneratedExact, generated.Strength.Source);
        Assert.Equal(119.08, generated.Strength.ExactEntropyBits!.Value, 2);
    }

    [Theory]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, false, true, false, true)]
    [InlineData(true, true, true, true, true)]
    public void 生成された文字はすべて候補集合に含まれ_エントロピーはL掛けるlog2A(bool up, bool low, bool dig, bool sym, bool omit)
    {
        var options = new PasswordOptions { Length = 64, IncludeUppercase = up, IncludeLowercase = low, IncludeDigits = dig, IncludeSymbols = sym, OmitConfusing = omit };
        var pool = PasswordGenerator.BuildPool(options);

        for (var i = 0; i < 20; i++)
        {
            var generated = _generator.Generate(options);
            Assert.All(generated.Text, c => Assert.Contains(c, pool));
            Assert.Equal(64 * Math.Log2(pool.Length), generated.Strength!.ExactEntropyBits!.Value, 6);
        }
    }

    [Fact]
    public void 紛らわしい文字を省くと該当文字が候補に無い()
    {
        var pool = PasswordGenerator.BuildPool(new PasswordOptions
        {
            IncludeUppercase = true, IncludeLowercase = true, IncludeDigits = true, IncludeSymbols = true, OmitConfusing = true,
        });
        Assert.DoesNotContain(pool, CharacterSets.ConfusingAlphanumerics.Contains);
        Assert.All(pool.Where(CharacterSets.IsSymbol), c => Assert.Contains(c, CharacterSets.SafeSymbols));
    }

    [Fact]
    public void 区画の連番と種類が正しい()
    {
        var generated = _generator.Generate(new PasswordOptions { Length = 10, IncludeUppercase = true, IncludeLowercase = true, IncludeDigits = true, IncludeSymbols = true });
        Assert.Equal(Enumerable.Range(1, 10), generated.Segments.Select(s => s.Index));
        Assert.All(generated.Segments, s =>
        {
            var c = s.Text[0];
            var expected = CharacterSets.IsDigit(c) ? CharacterKind.Digit
                : CharacterSets.IsSymbol(c) ? CharacterKind.Symbol
                : CharacterSets.IsUppercase(c) ? CharacterKind.Uppercase
                : CharacterKind.Lowercase;
            Assert.Equal(expected, s.Kind);
            Assert.Equal(string.Empty, s.Suffix);
        });
    }
}
