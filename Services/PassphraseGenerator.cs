using System.Security.Cryptography;
using System.Text;
using PasswordStrength;
using RandomPasswordGenerator.Models;

namespace RandomPasswordGenerator.Services;

public interface IPassphraseGenerator
{
    GeneratedPassword Generate(PassphraseOptions options);
}

/// <summary>
/// 辞書から単語を無作為に拾って覚えやすいパスワードを作る。
/// <para>
/// 生成方式: 各単語を辞書から独立・一様に選ぶ（同じ単語の重複を許す）。
/// 区切り文字は固定（ハイフンなど）か、区切りごとに候補集合から一様に選ぶ（ランダムな数字 / 数字と記号）。
/// 大文字化は決定論的な変換なのでエントロピーには数えない。
/// 生成方式を変えるときは、強度計算（<see cref="PassphraseParameters"/>）も必ず合わせること。
/// </para>
/// </summary>
public sealed class PassphraseGenerator(IWordList words, IPasswordStrengthService strength) : IPassphraseGenerator
{
    public GeneratedPassword Generate(PassphraseOptions options)
    {
        var wordCount = Math.Clamp(options.WordCount, PassphraseOptions.MinWordCount, PassphraseOptions.MaxWordCount);
        var separatorPool = SeparatorPool(options.Separator);
        var separatorCount = wordCount - 1;

        var segments = new PasswordSegment[wordCount];
        var text = new StringBuilder();
        for (var i = 0; i < wordCount; i++)
        {
            var word = ApplyCasing(words[RandomNumberGenerator.GetInt32(words.Count)], options);
            var suffix = i < separatorCount
                ? separatorPool[RandomNumberGenerator.GetInt32(separatorPool.Length)].ToString()
                : string.Empty;

            segments[i] = new PasswordSegment(word, i + 1, CharacterKind.Word, suffix);
            text.Append(word).Append(suffix);
        }

        var result = strength.EvaluateGeneratedPassphrase(new PassphraseParameters
        {
            DictionarySize = words.Count,
            WordCount = wordCount,
            AllowDuplicateWords = true,
            // 固定の区切り文字は候補数 1（寄与 0 bit）。ランダムなら候補数ぶんを加算する
            SeparatorCount = separatorCount,
            SeparatorPoolSize = separatorPool.Length,
        });

        return new GeneratedPassword(text.ToString(), segments, result);
    }

    /// <summary>区切り文字の候補。固定の区切りは要素 1 個の配列。</summary>
    internal static string SeparatorPool(PassphraseSeparator separator) => separator switch
    {
        PassphraseSeparator.Hyphen => "-",
        PassphraseSeparator.Space => " ",
        PassphraseSeparator.Period => ".",
        PassphraseSeparator.Comma => ",",
        PassphraseSeparator.Underscore => "_",
        PassphraseSeparator.RandomDigit => CharacterSets.Digits,
        PassphraseSeparator.RandomDigitOrSymbol => CharacterSets.DigitsAndSymbols,
        _ => "-",
    };

    internal static string ApplyCasing(string word, PassphraseOptions options)
    {
        // 両方: 先頭だけ大文字。大文字のみ: すべて大文字。それ以外（小文字のみ / 未指定）: すべて小文字
        if (options.IncludeUppercase && options.IncludeLowercase)
        {
            return word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
        }

        return options.IncludeUppercase ? word.ToUpperInvariant() : word.ToLowerInvariant();
    }
}
