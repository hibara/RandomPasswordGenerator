namespace PasswordStrength.Tests;

public class PassphraseEntropyCalculatorTests
{
    private const int DictionarySize = 5872;
    private readonly PassphraseEntropyCalculator _calc = new();

    [Theory]
    [InlineData(4, 50.08)]
    [InlineData(5, 62.60)]
    [InlineData(6, 75.12)]
    [InlineData(7, 87.64)]
    public void 五千八百七十二語_重複可(int words, double expected)
    {
        var bits = _calc.Calculate(new PassphraseParameters
        {
            DictionarySize = DictionarySize,
            WordCount = words,
            AllowDuplicateWords = true,
        });
        Assert.Equal(expected, bits, 2);
    }

    [Fact]
    public void 一語あたり約12_5196bit()
    {
        var bits = _calc.Calculate(new PassphraseParameters { DictionarySize = DictionarySize, WordCount = 1 });
        Assert.Equal(12.5196, bits, 4);
    }

    [Fact]
    public void 重複不可はN掛けるN引く1()
    {
        var bits = _calc.Calculate(new PassphraseParameters
        {
            DictionarySize = 10,
            WordCount = 3,
            AllowDuplicateWords = false,
        });
        Assert.Equal(Math.Log2(10 * 9 * 8), bits, 10);
    }

    [Fact]
    public void 固定の区切り文字は加算しない()
    {
        var fixedSep = _calc.Calculate(new PassphraseParameters
        {
            DictionarySize = DictionarySize,
            WordCount = 4,
            SeparatorCount = 3,
            SeparatorPoolSize = 1,
        });
        var noSep = _calc.Calculate(new PassphraseParameters { DictionarySize = DictionarySize, WordCount = 4 });
        Assert.Equal(noSep, fixedSep);
    }

    [Fact]
    public void ランダムな区切り文字は個数掛けるlog2候補数を加算()
    {
        var bits = _calc.Calculate(new PassphraseParameters
        {
            DictionarySize = DictionarySize,
            WordCount = 4,
            SeparatorCount = 3,
            SeparatorPoolSize = 10,
        });
        Assert.Equal(4 * Math.Log2(DictionarySize) + 3 * Math.Log2(10), bits, 10);
    }

    [Theory]
    [InlineData(0, 4, true, 0, 1)]
    [InlineData(100, 0, true, 0, 1)]
    [InlineData(3, 4, false, 0, 1)]
    [InlineData(100, 4, true, -1, 1)]
    [InlineData(100, 4, true, 3, 0)]
    public void 不正なパラメータは例外(int dict, int words, bool dup, int sepCount, int sepPool)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calc.Calculate(new PassphraseParameters
        {
            DictionarySize = dict,
            WordCount = words,
            AllowDuplicateWords = dup,
            SeparatorCount = sepCount,
            SeparatorPoolSize = sepPool,
        }));
    }
}
