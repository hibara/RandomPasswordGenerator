using System.Globalization;

namespace RandomPasswordGenerator.Services;

/// <summary>
/// 起動引数。現在は表示言語の指定だけ。
/// <para>
/// <c>--lang=en</c> / <c>--lang=ja</c>（<c>--lang en</c> の形も可）で UI 言語を固定する。
/// 未指定、または対応していない値のときは OS の表示言語に従う。
/// </para>
/// </summary>
public sealed record LaunchOptions(CultureInfo? UiCulture)
{
    /// <summary>対応している言語（リソースが用意されているもの）。</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = ["en", "ja"];

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        CultureInfo? culture = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            string? value = null;

            if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg["--lang=".Length..];
            }
            else if (string.Equals(arg, "--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                value = args[++i];
            }

            if (value is null)
            {
                continue;
            }

            // "en-US" のような地域付きも先頭の言語部分で判定する
            var language = value.Trim().Split('-', '_')[0].ToLowerInvariant();
            if (SupportedLanguages.Contains(language))
            {
                culture = CultureInfo.GetCultureInfo(language);
            }
            // 対応外の値は無視して OS の言語に従う
        }

        return new LaunchOptions(culture);
    }

    /// <summary>指定があれば、このプロセスの UI 言語を固定する。</summary>
    public void Apply()
    {
        if (UiCulture is null)
        {
            return;
        }

        CultureInfo.DefaultThreadCurrentUICulture = UiCulture;
        CultureInfo.CurrentUICulture = UiCulture;
    }
}
