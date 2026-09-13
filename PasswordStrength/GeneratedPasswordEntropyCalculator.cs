namespace PasswordStrength;

/// <summary>ランダム文字列パスワードのエントロピーを生成条件から正確に計算する。</summary>
public sealed class GeneratedPasswordEntropyCalculator
{
    /// <summary>
    /// Entropy = L × log2(A)。
    /// 各文字が独立・一様に選ばれている前提（<see cref="GeneratedPasswordParameters"/> 参照）。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">文字数が 0 以下、または文字集合サイズが 1 未満。</exception>
    public double Calculate(GeneratedPasswordParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(parameters.CharacterPoolSize, 1);

        return parameters.Length * Math.Log2(parameters.CharacterPoolSize);
    }
}
