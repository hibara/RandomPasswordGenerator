namespace PasswordStrength;

/// <summary>
/// 探索量（guesses）を UI 用の Score（0〜4）へ変換する。
/// 既定の境界は zxcvbn（zxcvbn-ts）と同一: 1e3+5, 1e6+5, 1e8+5, 1e10+5 guesses。
/// 正確な生成エントロピーも guesses = 2^bits とみなして同じ境界で変換するため、
/// 生成パスワードと自由入力パスワードのスコアが同じ意味を持つ。
/// </summary>
public sealed class PasswordStrengthScoreMapper
{
    /// <summary>zxcvbn と同じ境界（guesses の常用対数、昇順）。</summary>
    public static readonly IReadOnlyList<double> ZxcvbnThresholdsLog10 =
    [
        Math.Log10(1e3 + 5),
        Math.Log10(1e6 + 5),
        Math.Log10(1e8 + 5),
        Math.Log10(1e10 + 5),
    ];

    private readonly double[] _thresholdsLog10;

    /// <summary>zxcvbn と同じ境界を使う。</summary>
    public PasswordStrengthScoreMapper() : this(ZxcvbnThresholdsLog10) { }

    /// <summary>独自の境界（guesses の常用対数、昇順）を使う。要素数 = 最大スコア。</summary>
    /// <exception cref="ArgumentException">境界が空、有限値でない、または昇順でない。</exception>
    public PasswordStrengthScoreMapper(IEnumerable<double> thresholdsLog10)
    {
        ArgumentNullException.ThrowIfNull(thresholdsLog10);
        _thresholdsLog10 = [.. thresholdsLog10];

        if (_thresholdsLog10.Length == 0)
        {
            throw new ArgumentException("境界を 1 つ以上指定してください。", nameof(thresholdsLog10));
        }

        for (var i = 0; i < _thresholdsLog10.Length; i++)
        {
            // NaN は比較が常に false になり昇順検証をすり抜けるので、有限値だけを受け付ける
            if (!double.IsFinite(_thresholdsLog10[i]))
            {
                throw new ArgumentException("境界には有限値を指定してください。", nameof(thresholdsLog10));
            }

            if (i > 0 && _thresholdsLog10[i] <= _thresholdsLog10[i - 1])
            {
                throw new ArgumentException("境界は昇順に指定してください。", nameof(thresholdsLog10));
            }
        }
    }

    /// <summary>取り得る最大スコア。</summary>
    public int MaxScore => _thresholdsLog10.Length;

    /// <summary>log10(guesses) からスコアを求める。guesses が境界未満なら下のスコアになる（zxcvbn と同じ）。</summary>
    public int ScoreFromGuessesLog10(double guessesLog10)
    {
        if (double.IsNaN(guessesLog10))
        {
            return 0;
        }

        var score = 0;
        foreach (var threshold in _thresholdsLog10)
        {
            if (guessesLog10 >= threshold)
            {
                score++;
            }
        }

        return score;
    }

    /// <summary>エントロピー（bit）を guesses = 2^bits とみなしてスコアを求める。</summary>
    public int ScoreFromEntropyBits(double entropyBits) =>
        ScoreFromGuessesLog10(EntropyBitsToGuessesLog10(entropyBits));

    /// <summary>bit → log10(guesses)。2^bits = 10^(bits × log10(2))。</summary>
    public static double EntropyBitsToGuessesLog10(double entropyBits) => entropyBits * Math.Log10(2);

    /// <summary>log10(guesses) → bit 換算。log2(guesses) = log10(guesses) / log10(2)。</summary>
    public static double GuessesLog10ToBits(double guessesLog10) => guessesLog10 / Math.Log10(2);
}
