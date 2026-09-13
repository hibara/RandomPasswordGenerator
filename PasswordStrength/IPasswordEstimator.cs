namespace PasswordStrength;

/// <summary>
/// 自由入力パスワードの強度を推定する。実装を差し替えられるよう抽象化してある
/// （既定実装は <see cref="ZxcvbnPasswordEstimator"/>）。
/// </summary>
public interface IPasswordEstimator
{
    /// <summary>推定探索量の常用対数と、警告・提案を返す。</summary>
    /// <param name="password">評価対象。空文字列も許容する。</param>
    /// <param name="userInputs">ユーザー名など、含まれていたら弱いとみなすべき語。</param>
    PasswordEstimate Estimate(string password, IEnumerable<string>? userInputs = null);
}

/// <summary>推定器の生の結果。<see cref="PasswordStrengthService"/> が共通モデルへ変換する。</summary>
public sealed record PasswordEstimate(double GuessesLog10, string? Warning, IReadOnlyList<string> Suggestions);
