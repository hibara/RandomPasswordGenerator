namespace RandomPasswordGenerator.Models;

/// <summary>覚えやすいパスワードの単語の区切り。</summary>
public enum PassphraseSeparator
{
    Hyphen,
    Space,
    Period,
    Comma,
    Underscore,

    /// <summary>区切りごとにランダムな数字 1 文字。</summary>
    RandomDigit,

    /// <summary>区切りごとにランダムな数字または記号 1 文字。</summary>
    RandomDigitOrSymbol,
}
