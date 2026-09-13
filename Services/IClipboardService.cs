using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace RandomPasswordGenerator.Services;

public interface IClipboardService
{
    /// <summary>クリップボードへ書き込む。書き込めたら true、クリップボードが使えなければ false。</summary>
    Task<bool> SetTextAsync(string text);
}

/// <summary>Avalonia の TopLevel からクリップボードを取得して書き込む。</summary>
public sealed class AvaloniaClipboardService(TopLevel topLevel) : IClipboardService
{
    public async Task<bool> SetTextAsync(string text)
    {
        if (topLevel.Clipboard is not { } clipboard)
        {
            return false;
        }

        await clipboard.SetTextAsync(text);
        return true;
    }
}
