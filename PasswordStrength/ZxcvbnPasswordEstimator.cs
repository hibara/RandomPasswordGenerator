using Zxcvbn;

namespace PasswordStrength;

/// <summary>
/// Zxcvbn.NET（zxcvbn-ts 移植）による推定。Zxcvbn の型はこのクラスの外へ出さない。
/// <see cref="ZxcvbnAnalyzer"/> は不変・スレッドセーフなので 1 インスタンスを共有する。
/// </summary>
public sealed class ZxcvbnPasswordEstimator : IPasswordEstimator
{
    // 辞書の読み込みにコストがかかるため、最初に推定が必要になるまで生成しない
    private readonly Lazy<ZxcvbnAnalyzer> _analyzer;

    /// <summary>既定の辞書（英語の一般的なパスワード・単語・人名など）で構成する。</summary>
    public ZxcvbnPasswordEstimator() : this(new ZxcvbnOptions()) { }

    private ZxcvbnPasswordEstimator(ZxcvbnOptions options) =>
        _analyzer = new Lazy<ZxcvbnAnalyzer>(() => new ZxcvbnAnalyzer(options), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 頻度順（よく使われる順）に並んだカスタム辞書を追加して構成する。
    /// Zxcvbn.NET は配列の位置を順位（rank）として使うため、頻度順でない単語一覧を渡してはならない。
    /// </summary>
    public static ZxcvbnPasswordEstimator WithRankedDictionaries(
        IReadOnlyDictionary<string, IEnumerable<string>> rankedDictionaries)
    {
        ArgumentNullException.ThrowIfNull(rankedDictionaries);

        var options = new ZxcvbnOptions();
        foreach (var (name, words) in rankedDictionaries)
        {
            options.AddDictionary(name, words);
        }

        return new ZxcvbnPasswordEstimator(options);
    }

    /// <inheritdoc />
    public PasswordEstimate Estimate(string password, IEnumerable<string>? userInputs = null)
    {
        ArgumentNullException.ThrowIfNull(password);

        var result = _analyzer.Value.Evaluate(password, userInputs);
        return new PasswordEstimate(
            result.GuessesLog10,
            string.IsNullOrEmpty(result.Feedback.Warning) ? null : result.Feedback.Warning,
            [.. result.Feedback.Suggestions]);
    }
}
