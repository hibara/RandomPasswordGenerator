using Avalonia.Controls;
using Avalonia.Input;
using RandomPasswordGenerator.ViewModels;

namespace RandomPasswordGenerator.Views;

public partial class MainWindow : ShadUI.Window
{
    public MainWindow()
    {
        InitializeComponent();

        // ShadUI（SaveWindowState="True"）は XAML 読み込み時に前回の位置・サイズを復元するが、
        // CenterScreen だと表示時に位置が上書きされてしまう。保存済みの状態があるときだけ手動配置にする。
        WindowStartupLocation = File.Exists(ShadUiWindowStatePath)
            ? WindowStartupLocation.Manual
            : WindowStartupLocation.CenterScreen;
    }

    /// <summary>ShadUI がウィンドウ状態を保存するファイル（キーはアセンブリ名）。</summary>
    private static string ShadUiWindowStatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShadUI",
        $"shadui_{typeof(MainWindow).Assembly.GetName().Name}.txt");

    /// <summary>パスワード表示領域のサイズ変更を ViewModel に伝え、文字サイズを再計算させる。</summary>
    private void OnPasswordViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ScrollViewer viewport)
        {
            // 内側の Border の Padding(4) 分を差し引く
            viewModel.UpdateViewport(viewport.Bounds.Width - 8, viewport.Bounds.Height - 8);
        }
    }

    /// <summary>パスワード表示領域のクリック（タップ）でクリップボードにコピーする。</summary>
    private void OnPasswordAreaTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CopyCommand.Execute(null);
        }
    }
}
