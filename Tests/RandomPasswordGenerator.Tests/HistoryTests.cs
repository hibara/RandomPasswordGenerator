using PasswordStrength;
using RandomPasswordGenerator.Models;
using RandomPasswordGenerator.Services;
using RandomPasswordGenerator.ViewModels;

namespace RandomPasswordGenerator.Tests;

public class HistoryTests
{
    private static readonly PasswordStrengthService Strength = new();
    private static readonly EmbeddedWordList Words = EmbeddedWordList.Load();

    private sealed class FakeClipboard : IClipboardService
    {
        public List<string> Copied { get; } = [];
        public Task<bool> SetTextAsync(string text) { Copied.Add(text); return Task.FromResult(true); }
    }

    private static (MainWindowViewModel Vm, FakeClipboard Clipboard) Create(AppSettings? settings = null)
    {
        var clipboard = new FakeClipboard();
        var vm = new MainWindowViewModel(
            new PasswordGenerator(Strength),
            new PassphraseGenerator(Words, Strength),
            new PinGenerator(),
            clipboard,
            settings ?? new AppSettings { HistoryEnabled = true, HistoryCapacity = 3 });
        return (vm, clipboard);
    }

    [Fact]
    public void 件数ぶんの行が常にあり_足りない分は空行()
    {
        var (vm, _) = Create();
        Assert.Equal(3, vm.HistoryRows.Count);
        Assert.All(vm.HistoryRows, r => Assert.True(r.IsEmpty));
        Assert.Equal([1, 2, 3], vm.HistoryRows.Select(r => r.Index));
    }

    [Fact]
    public async Task コピーすると先頭に追加される()
    {
        var (vm, clipboard) = Create();
        await vm.CopyCommand.ExecuteAsync(null);
        var first = vm.Password;
        vm.GenerateCommand.Execute(null);
        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal(2, clipboard.Copied.Count);
        Assert.Equal(vm.Password, vm.HistoryRows[0].Text);
        Assert.Equal(first, vm.HistoryRows[1].Text);
        Assert.True(vm.HistoryRows[2].IsEmpty);
        Assert.Equal(GeneratorMode.Random, vm.HistoryRows[0].Entry!.Kind);
    }

    [Fact]
    public void 同じパスワードは重複せず先頭へ移る()
    {
        var (vm, _) = Create();
        vm.RecordHistory("aaa", GeneratorMode.Random);
        vm.RecordHistory("bbb", GeneratorMode.Pin);
        vm.RecordHistory("aaa", GeneratorMode.Random);

        Assert.Equal(["aaa", "bbb", ""], vm.HistoryRows.Select(r => r.Text));
    }

    [Fact]
    public void 件数を超えた古いものは消える_件数を減らしても切り詰める()
    {
        var (vm, _) = Create();
        foreach (var t in new[] { "1", "2", "3", "4" }) vm.RecordHistory(t, GeneratorMode.Pin);
        Assert.Equal(["4", "3", "2"], vm.HistoryRows.Select(r => r.Text));

        vm.HistoryCapacity = 2;
        Assert.Equal(["4", "3"], vm.HistoryRows.Select(r => r.Text));

        vm.HistoryCapacity = 4;
        Assert.Equal(["4", "3", "", ""], vm.HistoryRows.Select(r => r.Text));
    }

    [Fact]
    public void OFFにしても一覧は消えず_新しいコピーだけ記録されない()
    {
        var (vm, _) = Create();
        vm.RecordHistory("secret", GeneratorMode.Random);

        vm.HistoryEnabled = false;
        Assert.Equal("secret", vm.HistoryRows[0].Text);

        vm.RecordHistory("another", GeneratorMode.Random);
        Assert.Equal(["secret", "", ""], vm.HistoryRows.Select(r => r.Text));

        vm.HistoryEnabled = true;
        vm.RecordHistory("another", GeneratorMode.Random);
        Assert.Equal(["another", "secret", ""], vm.HistoryRows.Select(r => r.Text));
    }

    [Fact]
    public async Task 履歴の行をクリックすると再コピーされ_履歴の順序は変わらない()
    {
        var (vm, clipboard) = Create();
        vm.RecordHistory("first", GeneratorMode.Random);
        vm.RecordHistory("second", GeneratorMode.Random);

        await vm.CopyHistoryCommand.ExecuteAsync(vm.HistoryRows[1]);
        Assert.Equal(["first"], clipboard.Copied);
        Assert.Equal(["second", "first", ""], vm.HistoryRows.Select(r => r.Text));

        await vm.CopyHistoryCommand.ExecuteAsync(vm.HistoryRows[2]); // 空行
        Assert.Single(clipboard.Copied);
    }

    [Fact]
    public void クリアで全部消える()
    {
        var (vm, _) = Create();
        vm.RecordHistory("x", GeneratorMode.Random);
        vm.ClearHistoryCommand.Execute(null);
        Assert.All(vm.HistoryRows, r => Assert.True(r.IsEmpty));
    }

    [Fact]
    public void 設定には履歴の内容は含まれない_履歴はメモリ上だけ()
    {
        var (vm, _) = Create();
        vm.RecordHistory("secret", GeneratorMode.Random);
        var settings = vm.ToSettings();
        Assert.True(settings.HistoryEnabled);
        Assert.Equal(3, settings.HistoryCapacity);
        Assert.DoesNotContain(settings.GetType().GetProperties(), p => p.PropertyType != typeof(bool) && p.PropertyType != typeof(int) && p.PropertyType != typeof(GeneratorMode) && p.PropertyType != typeof(PassphraseSeparator));
    }

    [Fact]
    public void 履歴タブでは生成も強度表示もしない()
    {
        var (vm, _) = Create();
        var before = vm.Password;
        vm.ModeIndex = (int)GeneratorMode.History;
        Assert.True(vm.IsHistoryMode);
        Assert.False(vm.IsPasswordVisible);
        Assert.False(vm.IsStrengthVisible);
        Assert.Equal(before, vm.Password);
    }
}
