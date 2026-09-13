namespace PasswordStrength.Tests;

public class PasswordStrengthScoreMapperTests
{
    private readonly PasswordStrengthScoreMapper _mapper = new();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2.9, 0)]
    [InlineData(3.0, 0)]      // ちょうど 1e3 は zxcvbn と同じく下のスコア（境界は 1e3+5）
    [InlineData(3.1, 1)]
    [InlineData(5.9, 1)]
    [InlineData(6.1, 2)]
    [InlineData(7.9, 2)]
    [InlineData(8.1, 3)]
    [InlineData(9.9, 3)]
    [InlineData(10.1, 4)]
    [InlineData(40, 4)]
    public void zxcvbnと同じ境界でスコアになる(double guessesLog10, int expected)
    {
        Assert.Equal(expected, _mapper.ScoreFromGuessesLog10(guessesLog10));
    }

    [Theory]
    [InlineData(9.0, 0)]     // 2^9 = 512 < 1e3
    [InlineData(10.0, 1)]    // 1024 > 1e3+5
    [InlineData(19.9, 1)]    // 2^19.9 ≒ 978k < 1e6
    [InlineData(20.0, 2)]    // 2^20 = 1,048,576 > 1e6+5
    [InlineData(26.6, 3)]    // 2^26.6 ≒ 1.02e8
    [InlineData(33.3, 4)]    // 2^33.3 ≒ 1.06e10
    [InlineData(50.08, 4)]
    [InlineData(119.08, 4)]
    public void エントロピーから同じ境界でスコアになる(double bits, int expected)
    {
        Assert.Equal(expected, _mapper.ScoreFromEntropyBits(bits));
    }

    [Fact]
    public void bitとlog10guessesの相互変換()
    {
        Assert.Equal(3.0103, PasswordStrengthScoreMapper.EntropyBitsToGuessesLog10(10), 4);
        Assert.Equal(10, PasswordStrengthScoreMapper.GuessesLog10ToBits(3.0103), 3);
    }

    [Fact]
    public void 独自の境界を指定できる()
    {
        var mapper = new PasswordStrengthScoreMapper([10, 20]);
        Assert.Equal(2, mapper.MaxScore);
        Assert.Equal(0, mapper.ScoreFromGuessesLog10(5));
        Assert.Equal(1, mapper.ScoreFromGuessesLog10(15));
        Assert.Equal(2, mapper.ScoreFromGuessesLog10(25));
    }

    [Fact]
    public void NaNはスコア0()
    {
        Assert.Equal(0, _mapper.ScoreFromGuessesLog10(double.NaN));
    }

    [Fact]
    public void 空や非昇順の境界は例外()
    {
        Assert.Throws<ArgumentException>(() => new PasswordStrengthScoreMapper([]));
        Assert.Throws<ArgumentException>(() => new PasswordStrengthScoreMapper([5, 5]));
        Assert.Throws<ArgumentException>(() => new PasswordStrengthScoreMapper([5, 3]));
    }

    [Fact]
    public void NaNや無限大の境界は例外()
    {
        Assert.Throws<ArgumentException>(() => new PasswordStrengthScoreMapper([double.NaN]));
        Assert.Throws<ArgumentException>(() => new PasswordStrengthScoreMapper([3, double.NaN, 8]));
        Assert.Throws<ArgumentException>(() => new PasswordStrengthScoreMapper([3, double.PositiveInfinity]));
    }
}
