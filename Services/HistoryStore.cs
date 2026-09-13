namespace RandomPasswordGenerator.Services;

/// <summary>
/// コピー履歴はメモリ上にのみ保持し、ファイルには保存しない（平文でパスワードが残るのを避けるため）。
/// 以前のバージョンが設定フォルダーに書いた history.json が残っていれば、起動時に削除する。
/// </summary>
public static class HistoryStore
{
    private static readonly string LegacyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RandomPasswordGenerator",
        "history.json");

    /// <summary>旧バージョンの履歴ファイルが残っていれば消す。</summary>
    public static void DeleteLegacyFile() => DeleteIfExists(LegacyPath);

    internal static void DeleteIfExists(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 消せなくても本体の動作には影響させない
        }
    }
}
