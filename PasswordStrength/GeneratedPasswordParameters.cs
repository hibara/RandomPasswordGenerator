namespace PasswordStrength;

/// <summary>
/// アプリ自身が生成したランダム文字列パスワードの生成条件。
/// <para>
/// 前提: <see cref="Length"/> 文字それぞれを、サイズ <see cref="CharacterPoolSize"/> の文字集合から
/// 独立かつ一様に（暗号学的に安全な乱数で）選んでいること。
/// 「各文字種を最低 1 文字含める」などの強制条件がある生成方式には、このモデルをそのまま使ってはならない
/// （生成空間が狭まるため過大評価になる）。
/// </para>
/// </summary>
public sealed record GeneratedPasswordParameters
{
    /// <summary>パスワードの文字数。</summary>
    public required int Length { get; init; }

    /// <summary>1 文字を選ぶ候補文字集合のサイズ（例: 英大小文字 + 数字なら 62）。</summary>
    public required int CharacterPoolSize { get; init; }
}
