namespace RandomPasswordGenerator.Models;

/// <summary>パスワード生成の条件。</summary>
public sealed record PasswordOptions
{
    public const int MinLength = 1;
    public const int MaxLength = 64;
    public const int DefaultLength = 16;

    public int Length { get; init; } = DefaultLength;
    public bool IncludeUppercase { get; init; } = true;
    public bool IncludeLowercase { get; init; } = true;
    public bool IncludeDigits { get; init; } = true;
    public bool IncludeSymbols { get; init; }
    public bool OmitConfusing { get; init; }
}
