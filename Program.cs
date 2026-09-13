using Avalonia;
using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator;

internal static class Program
{
    // Avalonia の初期化前に Avalonia / サードパーティ API を呼ばないこと。
    [STAThread]
    public static void Main(string[] args)
    {
        // 表示言語: --lang=en / --lang=ja で固定。未指定なら OS の言語に従う。
        // 文字列リソースは起動時の UI カルチャで解決されるため、Avalonia の初期化より前に設定する
        LaunchOptions.Parse(args).Apply();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // デザイナー（プレビュー）からも参照されるため削除しないこと。
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
