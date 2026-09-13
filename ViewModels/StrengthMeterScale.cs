namespace RandomPasswordGenerator.ViewModels;

/// <summary>
/// 強度メーターの塗り幅（0〜1）を決める表示ポリシー（このアプリの UI 側の都合）。
/// zxcvbn のスコア境界（1e3 / 1e6 / 1e8 / 1e10 guesses）がメーターの 20% / 40% / 60% / 80% に来るよう
/// 区分線形に変換し、1e20 guesses（≒ 66 bit）以上で満タンにする。
/// これによりスコアの色・文言とメーターの長さが矛盾せず、かつ語数や文字数を増やすほど伸びる。
/// </summary>
public static class StrengthMeterScale
{
    // (log10(guesses), 塗り幅) の折れ点
    private static readonly (double Log10, double Fraction)[] Knots =
    [
        (0, 0.0),
        (3, 0.2),
        (6, 0.4),
        (8, 0.6),
        (10, 0.8),
        (20, 1.0),
    ];

    public static double Fraction(double guessesLog10)
    {
        if (double.IsNaN(guessesLog10) || guessesLog10 <= 0)
        {
            return 0;
        }

        for (var i = 1; i < Knots.Length; i++)
        {
            var (x1, y1) = Knots[i];
            if (guessesLog10 <= x1)
            {
                var (x0, y0) = Knots[i - 1];
                return y0 + (y1 - y0) * (guessesLog10 - x0) / (x1 - x0);
            }
        }

        return 1;
    }
}
