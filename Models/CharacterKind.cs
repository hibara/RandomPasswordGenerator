namespace RandomPasswordGenerator.Models;

/// <summary>パスワードを構成する要素の種類。表示色の切り替えに使う。</summary>
public enum CharacterKind
{
    Lowercase,
    Uppercase,
    Digit,
    Symbol,

    /// <summary>覚えやすいパスワードの単語 1 つ。</summary>
    Word,
}
