using RandomPasswordGenerator.Models;
using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator.Tests;

public class HistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rpg-history-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "history.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void 保存して読み込むと同じ内容()
    {
        var store = new HistoryStore(FilePath);
        var entries = new[]
        {
            new HistoryEntry("Kx7#mQ2p", GeneratorMode.Random, new DateTime(2026, 9, 13, 8, 0, 0)),
            new HistoryEntry("sakura-neko", GeneratorMode.Passphrase, new DateTime(2026, 9, 13, 8, 1, 0)),
        };

        store.Save(entries);
        var loaded = store.Load();

        Assert.Equal(entries, loaded);
        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Unix系では所有者のみ読み書き可()
    {
        if (OperatingSystem.IsWindows()) return;

        var store = new HistoryStore(FilePath);
        store.Save([new HistoryEntry("x", GeneratorMode.Pin, DateTime.Now)]);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
    }

    [Fact]
    public void 削除するとファイルが無くなり_読み込みは空()
    {
        var store = new HistoryStore(FilePath);
        store.Save([new HistoryEntry("x", GeneratorMode.Pin, DateTime.Now)]);
        store.Delete();

        Assert.False(File.Exists(FilePath));
        Assert.Empty(store.Load());
    }

    [Fact]
    public void 空の履歴を保存するとファイルを残さない()
    {
        var store = new HistoryStore(FilePath);
        store.Save([new HistoryEntry("x", GeneratorMode.Pin, DateTime.Now)]);
        store.Save([]);

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void 壊れたファイルは空として扱う()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Empty(new HistoryStore(FilePath).Load());
    }

    [Fact]
    public void ファイルが無ければ空()
    {
        Assert.Empty(new HistoryStore(FilePath).Load());
    }
}
