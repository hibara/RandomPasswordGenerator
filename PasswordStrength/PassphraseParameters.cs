namespace PasswordStrength;

/// <summary>
/// アプリ自身が辞書から生成したパスフレーズの生成条件。
/// <para>
/// 前提: 各単語を、サイズ <see cref="DictionarySize"/> の辞書から一様に（暗号学的に安全な乱数で）選んでいること。
/// 固定の区切り文字・固定の大文字化などの決定論的な変換はエントロピーに寄与しない。
/// 区切り文字自体をランダムに選ぶ場合のみ <see cref="SeparatorCount"/> と <see cref="SeparatorPoolSize"/> で加算する。
/// </para>
/// </summary>
public sealed record PassphraseParameters
{
    /// <summary>辞書の単語数。</summary>
    public required int DictionarySize { get; init; }

    /// <summary>パスフレーズを構成する単語数。</summary>
    public required int WordCount { get; init; }

    /// <summary>
    /// 同じ単語を同一パスフレーズ内で再度選べるか。
    /// true なら N^k、false なら N × (N-1) × … × (N-k+1) を生成空間とみなす。
    /// </summary>
    public bool AllowDuplicateWords { get; init; } = true;

    /// <summary>ランダムに選ばれる区切り文字の個数。固定の区切り文字なら 0。</summary>
    public int SeparatorCount { get; init; }

    /// <summary>区切り文字 1 個あたりの候補数。固定の区切り文字なら 1（寄与 0 bit）。</summary>
    public int SeparatorPoolSize { get; init; } = 1;
}
