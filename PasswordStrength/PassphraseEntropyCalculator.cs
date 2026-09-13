namespace PasswordStrength;

/// <summary>辞書ベースのパスフレーズのエントロピーを生成条件から正確に計算する。</summary>
public sealed class PassphraseEntropyCalculator
{
    /// <summary>
    /// 重複可: k × log2(N)。重複不可: log2(N) + log2(N-1) + … + log2(N-k+1)。
    /// ランダム区切り文字: SeparatorCount × log2(SeparatorPoolSize) を加算（固定なら 0）。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 辞書サイズ・語数が 0 以下、重複不可で語数が辞書サイズを超える、区切りの個数が負、区切り候補数が 1 未満。
    /// </exception>
    public double Calculate(PassphraseParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.DictionarySize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.WordCount);
        ArgumentOutOfRangeException.ThrowIfNegative(parameters.SeparatorCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(parameters.SeparatorPoolSize, 1);

        double bits;
        if (parameters.AllowDuplicateWords)
        {
            bits = parameters.WordCount * Math.Log2(parameters.DictionarySize);
        }
        else
        {
            if (parameters.WordCount > parameters.DictionarySize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters),
                    "重複不可の場合、語数は辞書サイズを超えられません。");
            }

            bits = 0;
            for (var i = 0; i < parameters.WordCount; i++)
            {
                bits += Math.Log2(parameters.DictionarySize - i);
            }
        }

        bits += parameters.SeparatorCount * Math.Log2(parameters.SeparatorPoolSize);
        return bits;
    }
}
