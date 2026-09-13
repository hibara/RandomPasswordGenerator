namespace RandomPasswordGenerator.Services;

/// <summary>
/// パスワードに使う文字集合の定義。
/// 元ネタ: _資料/password_strings.txt および旧 Electron 版 index.js。
/// </summary>
public static class CharacterSets
{
    public const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Digits = "0123456789";

    /// <summary>ASCII の記号 32 種（旧アプリは「‘」「–」が混入していたので ASCII に正規化）。</summary>
    public const string Symbols = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    /// <summary>
    /// 「紛らわしい文字を省く」で除外する英数字。旧アプリの定義をそのまま踏襲。
    /// 0/O/o、1/I/l、2/Z/z、6/b、9/g と、大文字・小文字の形が同じで区別しにくい英字。
    /// </summary>
    public const string ConfusingAlphanumerics = "0Oo1Il2Zz6b9gCcKkPpSsUuVvWwXxYy";

    /// <summary>
    /// 「紛らわしい文字を省く」を有効にしたときでも残す記号。
    /// password_strings.txt の「紛らわしい文字を排除したパスワード文字列」に基づく。
    /// </summary>
    public const string SafeSymbols = "!@#$%&*?";

    /// <summary>覚えやすいパスワードの「ランダムな数字と記号」区切りの候補（10 + 32 = 42 文字）。</summary>
    public const string DigitsAndSymbols = Digits + Symbols;

    public static bool IsDigit(char c) => Digits.Contains(c);
    public static bool IsSymbol(char c) => Symbols.Contains(c);
    public static bool IsLowercase(char c) => Lowercase.Contains(c);
    public static bool IsUppercase(char c) => Uppercase.Contains(c);
}
