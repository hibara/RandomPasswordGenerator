namespace RandomPasswordGenerator.Models;

/// <summary>
/// 表示用のパスワードの 1 区画。ランダムなパスワード／暗証番号では 1 文字、覚えやすいパスワードでは 1 単語。
/// 位置（1 始まり）と種類を持ち、単語の場合は後ろに付く区切り文字も持つ。
/// </summary>
public sealed record PasswordSegment(string Text, int Index, CharacterKind Kind, string Suffix = "")
{
    /// <summary>1 文字の区画を作る。</summary>
    public PasswordSegment(char value, int index, CharacterKind kind) : this(value.ToString(), index, kind) { }

    /// <summary>旧アプリの奇数セル背景（nth-child(odd)）を再現するためのフラグ。</summary>
    public bool IsOdd => Index % 2 == 1;

    public bool IsLowercase => Kind == CharacterKind.Lowercase;
    public bool IsUppercase => Kind == CharacterKind.Uppercase;
    public bool IsDigit => Kind == CharacterKind.Digit;
    public bool IsSymbol => Kind == CharacterKind.Symbol;
    public bool IsWord => Kind == CharacterKind.Word;

    /// <summary>区切り文字が数字なら数字の色、それ以外（記号・空白など）は記号の色で表示する。</summary>
    public bool IsSuffixDigit => Suffix.Length == 1 && char.IsAsciiDigit(Suffix[0]);

    /// <summary>表示上の文字数（レイアウト計算用）。</summary>
    public int DisplayLength => Text.Length + Suffix.Length;
}
