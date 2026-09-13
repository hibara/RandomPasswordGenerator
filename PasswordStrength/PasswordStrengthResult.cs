namespace PasswordStrength;

/// <summary>
/// 強度評価の結果。評価方式（正確な計算 / 推定）を問わず UI へ渡す共通モデル。
/// 表示方法（色・文言・アイコン）は呼び出し側が決める。
/// </summary>
public sealed record PasswordStrengthResult
{
    /// <summary>評価方式。</summary>
    public required PasswordStrengthSource Source { get; init; }

    /// <summary>
    /// 生成アルゴリズムから正確に計算したエントロピー（bit）。
    /// <see cref="PasswordStrengthSource.GeneratedExact"/> のときのみ値を持つ。
    /// </summary>
    public double? ExactEntropyBits { get; init; }

    /// <summary>
    /// zxcvbn が推定した探索量（guesses）を bit 換算した値（log2(guesses)）。
    /// 生成エントロピーそのものではない。<see cref="PasswordStrengthSource.Estimated"/> のときのみ値を持つ。
    /// </summary>
    public double? EstimatedGuessBits { get; init; }

    /// <summary>
    /// 推定探索量の常用対数（log10(guesses)）。
    /// Score 算出の基準値で、正確な計算の場合はエントロピーから換算したもの。
    /// </summary>
    public required double GuessesLog10 { get; init; }

    /// <summary>zxcvbn 互換のスコア（0: 非常に脆弱 〜 4: 非常に強い）。</summary>
    public required int Score { get; init; }

    /// <summary>zxcvbn からの警告（英語）。推定評価で弱いときのみ。</summary>
    public string? Warning { get; init; }

    /// <summary>zxcvbn からの改善提案（英語）。推定評価で弱いときのみ。</summary>
    public IReadOnlyList<string> Suggestions { get; init; } = [];
}
