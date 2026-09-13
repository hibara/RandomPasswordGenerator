namespace RandomPasswordGenerator.Models;

/// <summary>覚えやすいパスワードの生成条件。</summary>
public sealed record PassphraseOptions
{
    public const int MinWordCount = 2;
    public const int MaxWordCount = 10;
    public const int DefaultWordCount = 4;

    public int WordCount { get; init; } = DefaultWordCount;
    public PassphraseSeparator Separator { get; init; } = PassphraseSeparator.Hyphen;

    /// <summary>
    /// 大文字を使う。小文字と両方なら各単語の先頭だけ大文字（Sakura-Neko）、
    /// 大文字だけならすべて大文字（SAKURA-NEKO）。
    /// </summary>
    public bool IncludeUppercase { get; init; }

    /// <summary>小文字を使う。小文字だけならすべて小文字（sakura-neko）。</summary>
    public bool IncludeLowercase { get; init; } = true;
}
