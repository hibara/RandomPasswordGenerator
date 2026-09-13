using System.Text.Json;
using System.Text.Json.Serialization;
using RandomPasswordGenerator.Models;

namespace RandomPasswordGenerator.Services;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<HistoryEntry>))]
internal sealed partial class HistoryJsonContext : JsonSerializerContext;

/// <summary>
/// コピー履歴の永続化。設定と同じフォルダーの history.json に保存する。
/// <para>
/// パスワードが平文で入るファイルなので、履歴が OFF のまま終了したときは <see cref="Delete"/> で必ず消す。
/// Unix 系では所有者のみ読み書き可（0600）にする。
/// </para>
/// </summary>
public sealed class HistoryStore
{
    private readonly string _path;

    public HistoryStore() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RandomPasswordGenerator",
        "history.json"))
    { }

    /// <summary>テスト用に保存先を指定できる。</summary>
    public HistoryStore(string path) => _path = path;

    public IReadOnlyList<HistoryEntry> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                using var stream = File.OpenRead(_path);
                var entries = JsonSerializer.Deserialize(stream, HistoryJsonContext.Default.ListHistoryEntry);
                return entries?.Where(e => !string.IsNullOrEmpty(e.Text)).ToArray() ?? [];
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 壊れたファイルは無視する
        }

        return [];
    }

    public void Save(IReadOnlyList<HistoryEntry> entries)
    {
        try
        {
            if (entries.Count == 0)
            {
                Delete();
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using (var stream = File.Create(_path))
            {
                JsonSerializer.Serialize(stream, entries.ToList(), HistoryJsonContext.Default.ListHistoryEntry);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 保存に失敗しても本体の動作には影響させない
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 消せなくても本体の動作には影響させない
        }
    }
}
