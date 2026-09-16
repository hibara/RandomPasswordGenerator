using Avalonia;
using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator;

internal static class Program
{
    // Avalonia の初期化前に Avalonia / サードパーティ API を呼ばないこと。
    [STAThread]
    public static int Main(string[] args)
    {
        // DLL ハイジャック対策。ほかの何よりも先に呼ぶ（文字列ハッシュの初期化すら bcrypt.dll を読むため、
        // これより前に別の処理を置かない）。詳細は WindowsDllSearchGuard を参照。
        // 検索順序を限定できなければ起動しない（黙って続けると対策前と同じ状態になる）
        if (!WindowsDllSearchGuard.Install())
        {
            WindowsDllSearchGuard.ShowInstallFailureDialog();
            return 1;
        }

        // 単一 EXE 配布で EXE の隣に DLL があれば、警告して起動しない（単一ファイル発行でなければ常に空）
        var foreignDlls = WindowsDllSearchGuard.FindForeignDlls();
        if (foreignDlls.Count > 0)
        {
            WindowsDllSearchGuard.ShowRefusalDialog(foreignDlls);
            return 1;
        }

        // 表示言語: --lang=en / --lang=ja で固定。未指定なら OS の言語に従う。
        // 文字列リソースは起動時の UI カルチャで解決されるため、Avalonia の初期化より前に設定する
        LaunchOptions.Parse(args).Apply();

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // デザイナー（プレビュー）からも参照されるため削除しないこと。
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
