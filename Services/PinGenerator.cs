using System.Security.Cryptography;
using RandomPasswordGenerator.Models;

namespace RandomPasswordGenerator.Services;

public interface IPinGenerator
{
    GeneratedPassword Generate(PinOptions options);
}

/// <summary>暗号学的に安全な乱数で数字のみの暗証番号を生成する。強度メーターの対象外なので強度は付けない。</summary>
public sealed class PinGenerator : IPinGenerator
{
    public GeneratedPassword Generate(PinOptions options)
    {
        var length = Math.Clamp(options.Length, PinOptions.MinLength, PinOptions.MaxLength);

        var segments = new PasswordSegment[length];
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            var c = CharacterSets.Digits[RandomNumberGenerator.GetInt32(CharacterSets.Digits.Length)];
            chars[i] = c;
            segments[i] = new PasswordSegment(c, i + 1, CharacterKind.Digit);
        }

        return new GeneratedPassword(new string(chars), segments, Strength: null);
    }
}
