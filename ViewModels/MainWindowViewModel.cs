using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordStrength;
using RandomPasswordGenerator.Models;
using RandomPasswordGenerator.Resources;
using RandomPasswordGenerator.Services;
using ShadUI;

namespace RandomPasswordGenerator.ViewModels;

/// <summary>区切りの選択肢（ComboBox 用）。</summary>
public sealed record SeparatorChoice(PassphraseSeparator Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>履歴の 1 行。件数に満たない行は <see cref="Entry"/> が null の空行。</summary>
public sealed record HistoryRow(int Index, HistoryEntry? Entry)
{
    public bool IsEmpty => Entry is null;
    public string Text => Entry?.Text ?? string.Empty;
    /// <summary>コピーした日時（例: 2026/09/13 08:58:33）。</summary>
    public string TimeText => Entry?.CopiedAt.ToString("yyyy/MM/dd HH:mm:ss") ?? string.Empty;

    public string KindLabel => Entry?.Kind switch
    {
        GeneratorMode.Random => Strings.HistoryKindRandom,
        GeneratorMode.Passphrase => Strings.HistoryKindPassphrase,
        GeneratorMode.Pin => Strings.HistoryKindPin,
        _ => string.Empty,
    };
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IPassphraseGenerator _passphraseGenerator;
    private readonly IPinGenerator _pinGenerator;
    private readonly IClipboardService _clipboard;

    public MainWindowViewModel(
        IPasswordGenerator passwordGenerator,
        IPassphraseGenerator passphraseGenerator,
        IPinGenerator pinGenerator,
        IClipboardService clipboard,
        AppSettings settings)
    {
        _passwordGenerator = passwordGenerator;
        _passphraseGenerator = passphraseGenerator;
        _pinGenerator = pinGenerator;
        _clipboard = clipboard;

        Separators =
        [
            new(PassphraseSeparator.Hyphen, Strings.SeparatorHyphen),
            new(PassphraseSeparator.Space, Strings.SeparatorSpace),
            new(PassphraseSeparator.Period, Strings.SeparatorPeriod),
            new(PassphraseSeparator.Comma, Strings.SeparatorComma),
            new(PassphraseSeparator.Underscore, Strings.SeparatorUnderscore),
            new(PassphraseSeparator.RandomDigit, Strings.SeparatorRandomDigit),
            new(PassphraseSeparator.RandomDigitOrSymbol, Strings.SeparatorRandomDigitOrSymbol),
        ];

        // 起動時に前回の条件を復元し、旧アプリと同様に即座にパスワードを 1 つ生成しておく
        _modeIndex = Enum.IsDefined(settings.Mode) ? (int)settings.Mode : 0;

        _length = Math.Clamp(settings.Length, PasswordOptions.MinLength, PasswordOptions.MaxLength);
        _includeUppercase = settings.IncludeUppercase;
        _includeLowercase = settings.IncludeLowercase;
        _includeDigits = settings.IncludeDigits;
        _includeSymbols = settings.IncludeSymbols;
        _omitConfusing = settings.OmitConfusing;

        // 設定ファイルが壊れていて文字種が 1 つも無い場合は既定に戻す
        if (SelectedKindCount == 0)
        {
            _includeUppercase = true;
            _includeLowercase = true;
        }

        _wordCount = Math.Clamp(settings.WordCount, PassphraseOptions.MinWordCount, PassphraseOptions.MaxWordCount);
        _selectedSeparator = Separators.FirstOrDefault(s => s.Value == settings.Separator) ?? Separators[0];
        _passphraseUppercase = settings.PassphraseUppercase;
        _passphraseLowercase = settings.PassphraseLowercase;
        if (!_passphraseUppercase && !_passphraseLowercase)
        {
            _passphraseLowercase = true;
        }

        _pinLength = Math.Clamp(settings.PinLength, PinOptions.MinLength, PinOptions.MaxLength);

        _historyEnabled = settings.HistoryEnabled;
        _historyCapacity = Math.Clamp(settings.HistoryCapacity, MinHistoryCapacity, MaxHistoryCapacity);
        RebuildHistoryRows();

        Generate();
    }

    /// <summary>トースト通知（ShadUI）。View の ToastHost にバインドする。</summary>
    public ToastManager ToastManager { get; } = new();

    // =====================================================================
    // 生成方式（タブ）
    // =====================================================================

    /// <summary>タブの選択位置。<see cref="GeneratorMode"/> の値と一致させている。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(Mode), nameof(IsRandomMode), nameof(IsPassphraseMode), nameof(IsPinMode), nameof(IsHistoryMode),
        nameof(IsPasswordVisible), nameof(IsStrengthVisible))]
    private int _modeIndex;

    public GeneratorMode Mode => Enum.IsDefined((GeneratorMode)ModeIndex) ? (GeneratorMode)ModeIndex : GeneratorMode.Random;

    public bool IsRandomMode => Mode == GeneratorMode.Random;
    public bool IsPassphraseMode => Mode == GeneratorMode.Passphrase;
    public bool IsPinMode => Mode == GeneratorMode.Pin;
    public bool IsHistoryMode => Mode == GeneratorMode.History;

    /// <summary>履歴タブではパスワード表示の代わりに履歴の一覧を出す。</summary>
    public bool IsPasswordVisible => !IsHistoryMode;

    partial void OnModeIndexChanged(int value) => Generate();

    // =====================================================================
    // ランダムなパスワード
    // =====================================================================

    public int MinLength => PasswordOptions.MinLength;
    public int MaxLength => PasswordOptions.MaxLength;

    [ObservableProperty]
    private int _length;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleUppercase), nameof(CanToggleLowercase), nameof(CanToggleDigits), nameof(CanToggleSymbols))]
    private bool _includeUppercase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleUppercase), nameof(CanToggleLowercase), nameof(CanToggleDigits), nameof(CanToggleSymbols))]
    private bool _includeLowercase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleUppercase), nameof(CanToggleLowercase), nameof(CanToggleDigits), nameof(CanToggleSymbols))]
    private bool _includeDigits;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleUppercase), nameof(CanToggleLowercase), nameof(CanToggleDigits), nameof(CanToggleSymbols))]
    private bool _includeSymbols;

    [ObservableProperty]
    private bool _omitConfusing;

    // 文字種を 1 つも選ばない状態にはできないようにする。
    // 選択中の文字種が残り 1 つになったら、そのチェックボックスは外せない（無効化する）。

    private int SelectedKindCount =>
        (IncludeUppercase ? 1 : 0) + (IncludeLowercase ? 1 : 0) + (IncludeDigits ? 1 : 0) + (IncludeSymbols ? 1 : 0);

    private bool CanToggle(bool isSelected) => !isSelected || SelectedKindCount > 1;

    public bool CanToggleUppercase => CanToggle(IncludeUppercase);
    public bool CanToggleLowercase => CanToggle(IncludeLowercase);
    public bool CanToggleDigits => CanToggle(IncludeDigits);
    public bool CanToggleSymbols => CanToggle(IncludeSymbols);

    // 旧アプリと同じく、条件を変えた瞬間にパスワードを再生成する
    partial void OnLengthChanged(int value) => Generate();
    partial void OnIncludeUppercaseChanged(bool value) => OnKindChanged(value, () => SelectedKindCount, v => IncludeUppercase = v);
    partial void OnIncludeLowercaseChanged(bool value) => OnKindChanged(value, () => SelectedKindCount, v => IncludeLowercase = v);
    partial void OnIncludeDigitsChanged(bool value) => OnKindChanged(value, () => SelectedKindCount, v => IncludeDigits = v);
    partial void OnIncludeSymbolsChanged(bool value) => OnKindChanged(value, () => SelectedKindCount, v => IncludeSymbols = v);
    partial void OnOmitConfusingChanged(bool value) => Generate();

    /// <summary>
    /// 文字種の変更時。最後の 1 つが外されて何も選ばれていない状態になったら元に戻す
    /// （通常は無効化で防いでいるが、念のための保険）。
    /// </summary>
    private void OnKindChanged(bool value, Func<int> selectedCount, Action<bool> restore)
    {
        if (!value && selectedCount() == 0)
        {
            restore(true);
            return;
        }

        Generate();
    }

    // =====================================================================
    // 覚えやすいパスワード
    // =====================================================================

    public int MinWordCount => PassphraseOptions.MinWordCount;
    public int MaxWordCount => PassphraseOptions.MaxWordCount;

    [ObservableProperty]
    private int _wordCount;

    public IReadOnlyList<SeparatorChoice> Separators { get; }

    [ObservableProperty]
    private SeparatorChoice _selectedSeparator;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTogglePassphraseUppercase), nameof(CanTogglePassphraseLowercase))]
    private bool _passphraseUppercase;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTogglePassphraseUppercase), nameof(CanTogglePassphraseLowercase))]
    private bool _passphraseLowercase;

    private int PassphraseCaseCount => (PassphraseUppercase ? 1 : 0) + (PassphraseLowercase ? 1 : 0);

    public bool CanTogglePassphraseUppercase => !PassphraseUppercase || PassphraseCaseCount > 1;
    public bool CanTogglePassphraseLowercase => !PassphraseLowercase || PassphraseCaseCount > 1;

    partial void OnWordCountChanged(int value) => Generate();
    partial void OnSelectedSeparatorChanged(SeparatorChoice value) => Generate();
    partial void OnPassphraseUppercaseChanged(bool value) => OnKindChanged(value, () => PassphraseCaseCount, v => PassphraseUppercase = v);
    partial void OnPassphraseLowercaseChanged(bool value) => OnKindChanged(value, () => PassphraseCaseCount, v => PassphraseLowercase = v);

    // =====================================================================
    // 暗証番号
    // =====================================================================

    public int MinPinLength => PinOptions.MinLength;
    public int MaxPinLength => PinOptions.MaxLength;

    [ObservableProperty]
    private int _pinLength;

    partial void OnPinLengthChanged(int value) => Generate();

    // =====================================================================
    // 生成結果
    // =====================================================================

    /// <summary>生成されたパスワード（文字列）。</summary>
    [ObservableProperty]
    private string _password = string.Empty;

    /// <summary>表示用の区画（1 文字ずつ、または 1 単語ずつ）。</summary>
    [ObservableProperty]
    private IReadOnlyList<PasswordSegment> _segments = [];

    // =====================================================================
    // 強度メーター
    // =====================================================================

    /// <summary>生成条件から正確に計算した強度。暗証番号では null。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(StrengthScore), nameof(StrengthLabel), nameof(StrengthBitsText), nameof(StrengthFraction),
        nameof(IsStrengthWeak), nameof(IsStrengthFair), nameof(IsStrengthGood), nameof(IsStrengthStrong))]
    private PasswordStrengthResult? _strength;

    /// <summary>暗証番号と履歴では強度メーターを出さない。</summary>
    public bool IsStrengthVisible => !IsPinMode && !IsHistoryMode;

    public int StrengthScore => Strength?.Score ?? 0;

    public string StrengthLabel => StrengthScore switch
    {
        0 => Strings.Strength0,
        1 => Strings.Strength1,
        2 => Strings.Strength2,
        3 => Strings.Strength3,
        _ => Strings.Strength4,
    };

    /// <summary>「95 bit」。推定値なら「推定 48 bit」。</summary>
    public string StrengthBitsText => Strength switch
    {
        { ExactEntropyBits: { } exact } => string.Format(Strings.BitsFormat, Math.Round(exact)),
        { EstimatedGuessBits: { } estimated } => string.Format(Strings.EstimatedBitsFormat, Math.Round(estimated)),
        _ => string.Empty,
    };

    /// <summary>メーターの塗り（0〜1）。</summary>
    public double StrengthFraction => Strength is null ? 0 : StrengthMeterScale.Fraction(Strength.GuessesLog10);

    // メーターの色分け（スコア 0〜1: 赤、2: 橙、3: 青、4: 緑）
    public bool IsStrengthWeak => StrengthScore <= 1;
    public bool IsStrengthFair => StrengthScore == 2;
    public bool IsStrengthGood => StrengthScore == 3;
    public bool IsStrengthStrong => StrengthScore >= 4;

    // =====================================================================
    // 表示レイアウト（ウィンドウサイズと区画数から動的に決める）
    // =====================================================================

    /// <summary>1 行に並べる区画数（UniformGrid の列数）。</summary>
    [ObservableProperty]
    private int _columns = 16;

    /// <summary>パスワード文字のフォントサイズ。</summary>
    [ObservableProperty]
    private double _charFontSize = 32;

    /// <summary>文字の下に出す連番のフォントサイズ。</summary>
    [ObservableProperty]
    private double _indexFontSize = 11;

    private double _viewportWidth;
    private double _viewportHeight;

    /// <summary>
    /// パスワード表示領域のサイズが変わったときに View から呼ぶ。
    /// 領域内にすべての区画が収まる最大のフォントサイズを求め、フォントサイズと列数を更新する。
    /// </summary>
    public void UpdateViewport(double width, double height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        RecalculateLayout();
    }

    private const double MinFontSize = 14;
    private const double MaxFontSize = 120;

    // 等幅フォント（RPG Mono = Courier Prime 改変版）の 1 文字幅 ≒ フォントサイズ × 0.6
    private const double MonoCharWidth = 0.6;

    private void RecalculateLayout()
    {
        var count = Segments.Count;
        if (count == 0 || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return;
        }

        if (IsPassphraseMode)
        {
            RecalculateFlowLayout();
            return;
        }

        // 区画の幅（フォントサイズ 1 あたり）。1 文字の区画は正方形に近い比率、単語の区画は文字数に比例
        var maxChars = Segments.Max(s => s.DisplayLength);
        var widthPerFont = maxChars <= 1 ? 1.333 : MonoCharWidth * maxChars + 0.6;

        var bestFont = 0.0;
        var bestColumns = count;

        // 行数を 1 から順に試し、幅・高さの両方に収まる最大のフォントサイズを選ぶ
        for (var rows = 1; rows <= count; rows++)
        {
            var columns = (count + rows - 1) / rows;
            var fontByWidth = _viewportWidth / columns / widthPerFont;
            // 区画の高さ ≒ 上下パディング 16 + 文字（1.3 倍）+ 連番（フォントの 1/3 の 1.3 倍）
            var fontByHeight = (_viewportHeight / rows - 16) / 1.733;
            var font = Math.Min(fontByWidth, fontByHeight);

            if (font > bestFont)
            {
                bestFont = font;
                bestColumns = columns;
            }

            if (fontByHeight < MinFontSize)
            {
                break;
            }
        }

        Columns = bestColumns;
        CharFontSize = Math.Round(Math.Clamp(bestFont, MinFontSize, MaxFontSize));
        IndexFontSize = Math.Max(9, Math.Round(CharFontSize / 3));
    }

    // 行の高さ ≒ フォントサイズ × 1.3
    private const double FlowLineHeight = 1.3;

    /// <summary>
    /// 覚えやすいパスワード用。単語（＋区切り）を一文として横に並べ、行末で折り返したときに
    /// 領域へ収まる最大のフォントサイズを求める（等幅フォント前提で折り返しを模擬する）。
    /// </summary>
    private void RecalculateFlowLayout()
    {
        var lengths = Segments.Select(s => s.DisplayLength).ToArray();

        for (var font = MaxFontSize; font >= MinFontSize; font -= 1)
        {
            var charWidth = font * MonoCharWidth;
            var lines = 1;
            var lineWidth = 0.0;
            foreach (var length in lengths)
            {
                var width = length * charWidth;
                if (lineWidth > 0 && lineWidth + width > _viewportWidth)
                {
                    lines++;
                    lineWidth = 0;
                }

                lineWidth += width;
            }

            // 1 単語が幅に収まらない、または行数分の高さ（文字 + 連番）が収まらないなら、もう少し小さくする
            var widest = lengths.Max() * charWidth;
            var indexFont = IndexFontFor(font);
            var lineHeight = font * FlowLineHeight + indexFont * FlowLineHeight + 2;
            if (widest <= _viewportWidth && lines * lineHeight <= _viewportHeight)
            {
                CharFontSize = font;
                IndexFontSize = indexFont;
                return;
            }
        }

        CharFontSize = MinFontSize;
        IndexFontSize = IndexFontFor(MinFontSize);
    }

    private static double IndexFontFor(double charFont) => Math.Max(9, Math.Round(charFont / 3));

    // =====================================================================
    // 履歴（クリップボードにコピーしたパスワード。メモリ上にのみ保持し、ファイルには保存しない）
    // =====================================================================

    public const int MinHistoryCapacity = 1;
    public const int MaxHistoryCapacity = 30;

    public int MinHistoryCapacityValue => MinHistoryCapacity;
    public int MaxHistoryCapacityValue => MaxHistoryCapacity;

    private readonly List<HistoryEntry> _history = [];

    /// <summary>
    /// 履歴を残すかどうか。OFF にしても一覧はすぐには消えず（うっかり消すのを防ぐ）、
    /// 新しいコピーが記録されなくなるだけ。履歴はアプリを終了すれば消える。
    /// </summary>
    [ObservableProperty]
    private bool _historyEnabled;

    /// <summary>保持する件数。表示は常にこの行数で、足りない分は空行。</summary>
    [ObservableProperty]
    private int _historyCapacity;

    /// <summary>表示用の行（常に <see cref="HistoryCapacity"/> 行）。</summary>
    [ObservableProperty]
    private IReadOnlyList<HistoryRow> _historyRows = [];

    partial void OnHistoryEnabledChanged(bool value)
    {
        // 一覧はそのまま。記録の可否だけが変わる
    }

    partial void OnHistoryCapacityChanged(int value)
    {
        TrimHistory();
        RebuildHistoryRows();
    }

    /// <summary>コピーしたパスワードを履歴の先頭に追加する（同じものが既にあれば先頭へ移す）。</summary>
    internal void RecordHistory(string text, GeneratorMode kind)
    {
        if (!HistoryEnabled || text.Length == 0)
        {
            return;
        }

        _history.RemoveAll(e => e.Text == text);
        _history.Insert(0, new HistoryEntry(text, kind, DateTime.Now));
        TrimHistory();
        RebuildHistoryRows();
    }

    private void TrimHistory()
    {
        if (_history.Count > HistoryCapacity)
        {
            _history.RemoveRange(HistoryCapacity, _history.Count - HistoryCapacity);
        }
    }

    private void RebuildHistoryRows()
    {
        HistoryRows = Enumerable.Range(0, HistoryCapacity)
            .Select(i => new HistoryRow(i + 1, i < _history.Count ? _history[i] : null))
            .ToArray();
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _history.Clear();
        RebuildHistoryRows();
    }

    /// <summary>履歴の行をクリックしたときに、そのパスワードを再度クリップボードにコピーする。</summary>
    [RelayCommand]
    private async Task CopyHistoryAsync(HistoryRow? row)
    {
        if (row?.Entry is null)
        {
            return;
        }

        if (!await _clipboard.SetTextAsync(row.Entry.Text))
        {
            ShowCopyFailedToast();
            return;
        }

        ShowCopiedToast();
    }

    // =====================================================================
    // コマンド
    // =====================================================================

    [RelayCommand]
    private void Generate()
    {
        // 履歴タブでは生成しない（直前のパスワードは他のタブに戻ったときに再生成される）
        if (IsHistoryMode)
        {
            return;
        }

        var generated = Mode switch
        {
            GeneratorMode.Passphrase => _passphraseGenerator.Generate(new PassphraseOptions
            {
                WordCount = WordCount,
                Separator = SelectedSeparator.Value,
                IncludeUppercase = PassphraseUppercase,
                IncludeLowercase = PassphraseLowercase,
            }),
            GeneratorMode.Pin => _pinGenerator.Generate(new PinOptions { Length = PinLength }),
            _ => _passwordGenerator.Generate(new PasswordOptions
            {
                Length = Length,
                IncludeUppercase = IncludeUppercase,
                IncludeLowercase = IncludeLowercase,
                IncludeDigits = IncludeDigits,
                IncludeSymbols = IncludeSymbols,
                OmitConfusing = OmitConfusing,
            }),
        };

        Segments = generated.Segments;
        Password = generated.Text;
        Strength = generated.Strength;
        RecalculateLayout();
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (Password.Length == 0)
        {
            return;
        }

        if (!await _clipboard.SetTextAsync(Password))
        {
            ShowCopyFailedToast();
            return;
        }

        RecordHistory(Password, Mode);
        ShowCopiedToast();
    }

    private void ShowCopiedToast() =>
        ToastManager.CreateToast(Strings.Copied)
            .WithContent(Strings.CopiedDetail)
            .WithDelay(2)
            .DismissOnClick()
            .ShowSuccess();

    private void ShowCopyFailedToast() =>
        ToastManager.CreateToast(Strings.CopyFailed)
            .WithContent(Strings.CopyFailedDetail)
            .WithDelay(3)
            .DismissOnClick()
            .ShowError();

    public AppSettings ToSettings() => new()
    {
        Mode = Mode,
        Length = Length,
        IncludeUppercase = IncludeUppercase,
        IncludeLowercase = IncludeLowercase,
        IncludeDigits = IncludeDigits,
        IncludeSymbols = IncludeSymbols,
        OmitConfusing = OmitConfusing,
        WordCount = WordCount,
        Separator = SelectedSeparator.Value,
        PassphraseUppercase = PassphraseUppercase,
        PassphraseLowercase = PassphraseLowercase,
        PinLength = PinLength,
        HistoryEnabled = HistoryEnabled,
        HistoryCapacity = HistoryCapacity,
    };
}
