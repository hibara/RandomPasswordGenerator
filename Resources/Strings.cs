using System.Globalization;
using System.Resources;

namespace RandomPasswordGenerator.Resources;

/// <summary>
/// Strings.resx / Strings.ja.resx への型付きアクセサ。
/// OS の表示言語（CurrentUICulture）に応じて自動的に切り替わる。
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("RandomPasswordGenerator.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string WindowTitle => Get(nameof(WindowTitle));
    public static string Generate => Get(nameof(Generate));
    public static string Copy => Get(nameof(Copy));
    public static string Length => Get(nameof(Length));
    public static string IncludeUppercase => Get(nameof(IncludeUppercase));
    public static string IncludeLowercase => Get(nameof(IncludeLowercase));
    public static string IncludeDigits => Get(nameof(IncludeDigits));
    public static string IncludeSymbols => Get(nameof(IncludeSymbols));
    public static string OmitConfusing => Get(nameof(OmitConfusing));
    public static string ClickToCopy => Get(nameof(ClickToCopy));
    public static string Copied => Get(nameof(Copied));
    public static string CopiedDetail => Get(nameof(CopiedDetail));
    public static string ModeRandom => Get(nameof(ModeRandom));
    public static string ModePassphrase => Get(nameof(ModePassphrase));
    public static string ModePin => Get(nameof(ModePin));
    public static string WordCount => Get(nameof(WordCount));
    public static string Separator => Get(nameof(Separator));
    public static string PinLength => Get(nameof(PinLength));
    public static string SeparatorHyphen => Get(nameof(SeparatorHyphen));
    public static string SeparatorSpace => Get(nameof(SeparatorSpace));
    public static string SeparatorPeriod => Get(nameof(SeparatorPeriod));
    public static string SeparatorComma => Get(nameof(SeparatorComma));
    public static string SeparatorUnderscore => Get(nameof(SeparatorUnderscore));
    public static string SeparatorRandomDigit => Get(nameof(SeparatorRandomDigit));
    public static string SeparatorRandomDigitOrSymbol => Get(nameof(SeparatorRandomDigitOrSymbol));
    public static string Regenerate => Get(nameof(Regenerate));
    public static string Strength => Get(nameof(Strength));
    public static string Strength0 => Get(nameof(Strength0));
    public static string Strength1 => Get(nameof(Strength1));
    public static string Strength2 => Get(nameof(Strength2));
    public static string Strength3 => Get(nameof(Strength3));
    public static string Strength4 => Get(nameof(Strength4));
    public static string BitsFormat => Get(nameof(BitsFormat));
    public static string EstimatedBitsFormat => Get(nameof(EstimatedBitsFormat));
    public static string ModeHistory => Get(nameof(ModeHistory));
    public static string HistoryEnabled => Get(nameof(HistoryEnabled));
    public static string HistoryCapacity => Get(nameof(HistoryCapacity));
    public static string HistoryClear => Get(nameof(HistoryClear));
    public static string HistoryDisabledHint => Get(nameof(HistoryDisabledHint));
    public static string HistoryHint => Get(nameof(HistoryHint));
    public static string HistoryKindRandom => Get(nameof(HistoryKindRandom));
    public static string HistoryKindPassphrase => Get(nameof(HistoryKindPassphrase));
    public static string HistoryKindPin => Get(nameof(HistoryKindPin));
}
