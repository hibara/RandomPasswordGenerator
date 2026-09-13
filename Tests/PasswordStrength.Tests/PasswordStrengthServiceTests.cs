using System.Diagnostics;

namespace PasswordStrength.Tests;

public class PasswordStrengthServiceTests
{
    // ZxcvbnAnalyzer は不変・スレッドセーフなので、テスト全体で 1 つを共有する
    private static readonly PasswordStrengthService Service = new();

    [Fact]
    public void 生成パスワードは正確なエントロピーで評価される()
    {
        var result = Service.EvaluateGeneratedPassword(new GeneratedPasswordParameters { Length = 20, CharacterPoolSize = 62 });

        Assert.Equal(PasswordStrengthSource.GeneratedExact, result.Source);
        Assert.NotNull(result.ExactEntropyBits);
        Assert.Equal(119.08, result.ExactEntropyBits.Value, 2);
        Assert.Null(result.EstimatedGuessBits);
        Assert.Equal(4, result.Score);
        Assert.Null(result.Warning);
        Assert.Empty(result.Suggestions);
    }

    [Theory]
    [InlineData(4, 50.08)]
    [InlineData(6, 75.12)]
    public void 生成パスフレーズは辞書サイズから正確に評価される(int words, double expected)
    {
        var result = Service.EvaluateGeneratedPassphrase(new PassphraseParameters { DictionarySize = 5872, WordCount = words });

        Assert.Equal(PasswordStrengthSource.GeneratedExact, result.Source);
        Assert.Equal(expected, result.ExactEntropyBits!.Value, 2);
        Assert.Equal(4, result.Score);
    }

    [Fact]
    public void 二語のパスフレーズはスコア2()
    {
        // 2 × 12.52 = 25.04 bit ≒ 3.4e7 guesses → 1e6 以上 1e8 未満
        var result = Service.EvaluateGeneratedPassphrase(new PassphraseParameters { DictionarySize = 5872, WordCount = 2 });
        Assert.Equal(2, result.Score);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("password123")]
    [InlineData("qwerty")]
    [InlineData("qwerty123")]
    [InlineData("12345678")]
    [InlineData("abcabcabc")]
    public void よくある弱いパスワードは高評価されない(string password)
    {
        var result = Service.EvaluateUserPassword(password);

        Assert.Equal(PasswordStrengthSource.Estimated, result.Source);
        Assert.Null(result.ExactEntropyBits);
        Assert.NotNull(result.EstimatedGuessBits);
        Assert.True(result.Score <= 1, $"{password} のスコアが {result.Score}（推定 {result.EstimatedGuessBits:F1} bit）");
    }

    [Fact]
    public void 弱いパスワードには警告か提案が付く()
    {
        var result = Service.EvaluateUserPassword("password");
        Assert.True(result.Warning is not null || result.Suggestions.Count > 0);
    }

    [Fact]
    public void 推定bitはlog2guessesである()
    {
        var result = Service.EvaluateUserPassword("correcthorsebatterystaple");
        Assert.Equal(result.GuessesLog10 / Math.Log10(2), result.EstimatedGuessBits!.Value, 6);
    }

    [Fact]
    public void 自由入力のスコアはzxcvbn自身のスコアと一致する()
    {
        // Score は共通の Mapper で付け直しているので、zxcvbn 本来のスコアと食い違わないことを確認する
        var analyzer = new Zxcvbn.ZxcvbnAnalyzer();
        foreach (var pw in new[] { "password", "Tr0ub4dour&3", "correcthorsebatterystaple", "zxcvbn", "aB3$kL9!mQ2#pR7&", "1qaz2wsx", "" })
        {
            var expected = analyzer.Evaluate(pw).Score;
            var actual = Service.EvaluateUserPassword(pw).Score;
            Assert.True(expected == actual, $"'{pw}': zxcvbn={expected}, service={actual}");
        }
    }

    [Fact]
    public void 空文字列で例外にならない()
    {
        var result = Service.EvaluateUserPassword(string.Empty);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void nullは例外()
    {
        Assert.Throws<ArgumentNullException>(() => Service.EvaluateUserPassword(null!));
    }

    [Fact]
    public void 長大な入力でも短時間で終わる()
    {
        var password = string.Concat(Enumerable.Repeat("correcthorse1!Zx", 2000)); // 32,000 文字
        var sw = Stopwatch.StartNew();
        var result = Service.EvaluateUserPassword(password);
        sw.Stop();

        Assert.Equal(PasswordStrengthSource.Estimated, result.Source);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"評価に {sw.ElapsedMilliseconds} ms かかった");
    }

    [Fact]
    public void 差し替えた推定器が使われる()
    {
        var service = new PasswordStrengthService(new FakeEstimator(7.5));
        var result = service.EvaluateUserPassword("anything");

        Assert.Equal(7.5, result.GuessesLog10);
        Assert.Equal(2, result.Score);
        Assert.Equal("w", result.Warning);
        Assert.Equal(["s"], result.Suggestions);
    }

    private sealed class FakeEstimator(double guessesLog10) : IPasswordEstimator
    {
        public PasswordEstimate Estimate(string password, IEnumerable<string>? userInputs = null) =>
            new(guessesLog10, "w", ["s"]);
    }
}
