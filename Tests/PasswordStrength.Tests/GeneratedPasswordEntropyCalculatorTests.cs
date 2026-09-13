namespace PasswordStrength.Tests;

public class GeneratedPasswordEntropyCalculatorTests
{
    private readonly GeneratedPasswordEntropyCalculator _calc = new();

    [Fact]
    public void 六十二文字から二十文字を一様独立に生成すると約119_08bit()
    {
        var bits = _calc.Calculate(new GeneratedPasswordParameters { Length = 20, CharacterPoolSize = 62 });
        Assert.Equal(119.08, bits, 2);
    }

    [Theory]
    [InlineData(1, 2, 1.0)]
    [InlineData(8, 10, 26.5754)]
    [InlineData(16, 94, 104.8734)]
    public void L掛けるlog2A(int length, int pool, double expected)
    {
        var bits = _calc.Calculate(new GeneratedPasswordParameters { Length = length, CharacterPoolSize = pool });
        Assert.Equal(expected, bits, 3);
    }

    [Fact]
    public void 候補が一文字ならエントロピーは0()
    {
        var bits = _calc.Calculate(new GeneratedPasswordParameters { Length = 10, CharacterPoolSize = 1 });
        Assert.Equal(0, bits);
    }

    [Theory]
    [InlineData(0, 62)]
    [InlineData(-1, 62)]
    [InlineData(10, 0)]
    public void 不正なパラメータは例外(int length, int pool)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calc.Calculate(new GeneratedPasswordParameters { Length = length, CharacterPoolSize = pool }));
    }
}
