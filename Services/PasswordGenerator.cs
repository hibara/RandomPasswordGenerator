using System.Security.Cryptography;
using PasswordStrength;
using RandomPasswordGenerator.Models;

namespace RandomPasswordGenerator.Services;

public interface IPasswordGenerator
{
    GeneratedPassword Generate(PasswordOptions options);
}

/// <summary>
/// 暗号学的に安全な乱数（<see cref="RandomNumberGenerator"/>）でランダムなパスワードを生成する。
/// <para>
/// 生成方式: 候補文字集合から各文字を独立・一様に選ぶだけで、文字種ごとの「最低 1 文字」の強制や
/// 再生成は行わない。したがってエントロピーは正確に L × log2(候補文字数) となる。
/// 生成方式を変えるときは、強度計算（<see cref="GeneratedPasswordParameters"/>）も必ず合わせること。
/// </para>
/// </summary>
public sealed class PasswordGenerator(IPasswordStrengthService strength) : IPasswordGenerator
{
    public GeneratedPassword Generate(PasswordOptions options)
    {
        var length = Math.Clamp(options.Length, PasswordOptions.MinLength, PasswordOptions.MaxLength);
        var pool = BuildPool(options);

        var segments = new PasswordSegment[length];
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            var c = pool[RandomNumberGenerator.GetInt32(pool.Length)];
            chars[i] = c;
            segments[i] = new PasswordSegment(c, i + 1, Classify(c));
        }

        var result = strength.EvaluateGeneratedPassword(new GeneratedPasswordParameters
        {
            Length = length,
            CharacterPoolSize = pool.Length,
        });

        return new GeneratedPassword(new string(chars), segments, result);
    }

    /// <summary>オプションに応じた候補文字の配列を組み立てる。</summary>
    internal static char[] BuildPool(PasswordOptions options)
    {
        var chars = new List<char>();

        if (options.IncludeUppercase)
        {
            chars.AddRange(CharacterSets.Uppercase);
        }

        if (options.IncludeLowercase)
        {
            chars.AddRange(CharacterSets.Lowercase);
        }

        if (options.IncludeDigits)
        {
            chars.AddRange(CharacterSets.Digits);
        }

        if (options.IncludeSymbols)
        {
            chars.AddRange(options.OmitConfusing ? CharacterSets.SafeSymbols : CharacterSets.Symbols);
        }

        if (options.OmitConfusing)
        {
            chars.RemoveAll(CharacterSets.ConfusingAlphanumerics.Contains);
        }

        // UI 側で「すべて外す」ことはできないようにしているが、念のため空にならない保険をかけておく
        return chars.Count > 0 ? [.. chars] : CharacterSets.Lowercase.ToCharArray();
    }

    private static CharacterKind Classify(char c)
    {
        if (CharacterSets.IsDigit(c)) return CharacterKind.Digit;
        if (CharacterSets.IsSymbol(c)) return CharacterKind.Symbol;
        if (CharacterSets.IsUppercase(c)) return CharacterKind.Uppercase;
        return CharacterKind.Lowercase;
    }
}
