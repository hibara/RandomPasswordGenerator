using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator.Tests;

public class HistoryStoreTests
{
    [Fact]
    public void 旧バージョンの履歴ファイルがあれば消す()
    {
        var path = Path.Combine(Path.GetTempPath(), "rpg-history-tests", Guid.NewGuid().ToString("N"), "history.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[]");

        HistoryStore.DeleteIfExists(path);

        Assert.False(File.Exists(path));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void ファイルが無くても例外にならない()
    {
        HistoryStore.DeleteIfExists(Path.Combine(Path.GetTempPath(), "rpg-history-tests", "missing", "history.json"));
    }
}
