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
/// Unix 系では作成時点から所有者のみ読み書き可（ディレクトリ 0700、ファイル 0600）にする。
/// 内容は平文の JSON（暗号化していない）。
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

            // 書き込み途中を他ユーザーに読まれないよう、ディレクトリとファイルは作成時点から制限した権限にする
            // （Windows には Unix の権限ビットが無いので、ユーザープロファイル配下の既定の ACL に任せる）
            var directory = Path.GetDirectoryName(_path)!;
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(directory);
            }
            else
            {
                Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(_path, options))
            {
                JsonSerializer.Serialize(stream, entries.ToList(), HistoryJsonContext.Default.ListHistoryEntry);
            }

            if (!OperatingSystem.IsWindows())
            {
                // 既存ファイルを上書きした場合に備えて、権限を改めて 0600 にそろえる
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
