using PasswordStrength;

namespace RandomPasswordGenerator.Models;

/// <summary>生成結果。文字列そのもの、表示用の区画、生成条件から求めた強度。</summary>
/// <param name="Text">パスワード文字列（コピーに使う）。</param>
/// <param name="Segments">表示用の区画。</param>
/// <param name="Strength">生成条件から正確に計算した強度。強度を表示しない方式（暗証番号）では null。</param>
public sealed record GeneratedPassword(
    string Text,
    IReadOnlyList<PasswordSegment> Segments,
    PasswordStrengthResult? Strength);
