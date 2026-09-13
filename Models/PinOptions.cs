namespace RandomPasswordGenerator.Models;

/// <summary>暗証番号の生成条件。</summary>
public sealed record PinOptions
{
    public const int MinLength = 3;
    public const int MaxLength = 16;
    public const int DefaultLength = 6;

    public int Length { get; init; } = DefaultLength;
}
