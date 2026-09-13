using System.Reflection;

namespace RandomPasswordGenerator.Services;

/// <summary>パスフレーズ用の単語辞書。</summary>
public interface IWordList
{
    int Count { get; }
    string this[int index] { get; }
}

/// <summary>
/// 実行ファイルに埋め込んだ日本語ローマ字辞書（dic/japanese_passphrase_romaji.txt、5,872 語）。
/// 1 行 1 語、小文字の ASCII 英字のみ。重複は取り除く。
/// </summary>
public sealed class EmbeddedWordList : IWordList
{
    public const string ResourceName = "RandomPasswordGenerator.dic.japanese_passphrase_romaji.txt";

    private readonly string[] _words;

    private EmbeddedWordList(string[] words) => _words = words;

    public int Count => _words.Length;
    public string this[int index] => _words[index];

    public static EmbeddedWordList Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"埋め込みリソースが見つかりません: {ResourceName}");
        using var reader = new StreamReader(stream);
        return FromText(reader.ReadToEnd());
    }

    /// <summary>テスト用にも使えるよう、テキストから組み立てる。</summary>
    public static EmbeddedWordList FromText(string text)
    {
        var words = text
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && line.All(char.IsAsciiLetterLower))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (words.Length == 0)
        {
            throw new InvalidOperationException("辞書に単語がありません。");
        }

        return new EmbeddedWordList(words);
    }
}
