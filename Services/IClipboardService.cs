using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace RandomPasswordGenerator.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

/// <summary>Avalonia の TopLevel からクリップボードを取得して書き込む。</summary>
public sealed class AvaloniaClipboardService(TopLevel topLevel) : IClipboardService
{
    public Task SetTextAsync(string text) =>
        topLevel.Clipboard is { } clipboard ? clipboard.SetTextAsync(text) : Task.CompletedTask;
}
