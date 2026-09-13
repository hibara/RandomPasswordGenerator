using RandomPasswordGenerator.ViewModels;

namespace RandomPasswordGenerator.Tests;

public class StrengthMeterScaleTests
{
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(3, 0.2)]
    [InlineData(6, 0.4)]
    [InlineData(8, 0.6)]
    [InlineData(10, 0.8)]
    [InlineData(15, 0.9)]
    [InlineData(20, 1.0)]
    [InlineData(35, 1.0)]
    public void スコア境界がメーターの20パーセント刻みに対応する(double log10, double expected)
    {
        Assert.Equal(expected, StrengthMeterScale.Fraction(log10), 6);
    }

    [Fact]
    public void 単調増加()
    {
        var prev = -1.0;
        for (var x = 0.0; x <= 40; x += 0.5)
        {
            var f = StrengthMeterScale.Fraction(x);
            Assert.True(f >= prev);
            prev = f;
        }
    }
}
