using System.Text.Json;
using System.Text.Json.Serialization;
using RandomPasswordGenerator.Models;

namespace RandomPasswordGenerator.Services;

/// <summary>永続化する設定（旧 Electron 版の electron-store 相当）。</summary>
public sealed class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter<GeneratorMode>))]
    public GeneratorMode Mode { get; set; } = GeneratorMode.Random;

    // ---- ランダムなパスワード ----
    public int Length { get; set; } = PasswordOptions.DefaultLength;
    public bool IncludeUppercase { get; set; } = true;
    public bool IncludeLowercase { get; set; } = true;
    public bool IncludeDigits { get; set; } = true;
    public bool IncludeSymbols { get; set; }
    public bool OmitConfusing { get; set; }

    // ---- 覚えやすいパスワード ----
    public int WordCount { get; set; } = PassphraseOptions.DefaultWordCount;

    [JsonConverter(typeof(JsonStringEnumConverter<PassphraseSeparator>))]
    public PassphraseSeparator Separator { get; set; } = PassphraseSeparator.Hyphen;

    public bool PassphraseUppercase { get; set; }
    public bool PassphraseLowercase { get; set; } = true;

    // ---- 暗証番号 ----
    public int PinLength { get; set; } = PinOptions.DefaultLength;

    // ---- 履歴（履歴の内容そのものは保存しない） ----
    public bool HistoryEnabled { get; set; } = true;
    public int HistoryCapacity { get; set; } = 10;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;

/// <summary>
/// 設定を JSON ファイルとして読み書きする。
/// 保存先: Windows は %APPDATA%、macOS は ~/Library/Application Support、Linux は ~/.config 配下の RandomPasswordGenerator/settings.json。
/// </summary>
public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RandomPasswordGenerator",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var stream = File.OpenRead(SettingsPath);
                return JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 壊れた設定ファイルは無視して既定値で起動する
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            using var stream = File.Create(SettingsPath);
            JsonSerializer.Serialize(stream, settings, AppSettingsJsonContext.Default.AppSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 保存に失敗しても本体の動作には影響させない
        }
    }
}
