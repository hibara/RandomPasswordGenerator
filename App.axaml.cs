using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PasswordStrength;
using RandomPasswordGenerator.Models;
using RandomPasswordGenerator.Services;
using RandomPasswordGenerator.ViewModels;
using RandomPasswordGenerator.Views;

namespace RandomPasswordGenerator;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();

            // 履歴は ON のときだけ読み込む。OFF で終了していれば消えているはずだが、念のため消す
            var historyStore = new HistoryStore();
            IReadOnlyList<HistoryEntry> history = [];
            if (settings.HistoryEnabled)
            {
                history = historyStore.Load();
            }
            else
            {
                historyStore.Delete();
            }

            // 強度評価: 生成条件から正確に計算する（zxcvbn は自由入力の推定時にだけ遅延初期化される）
            var strengthService = new PasswordStrengthService();

            var window = new MainWindow();
            var viewModel = new MainWindowViewModel(
                new PasswordGenerator(strengthService),
                new PassphraseGenerator(EmbeddedWordList.Load(), strengthService),
                new PinGenerator(),
                new AvaloniaClipboardService(window),
                settings,
                history);

            window.DataContext = viewModel;
            window.Closing += (_, _) =>
            {
                settingsService.Save(viewModel.ToSettings());

                // 履歴は ON なら保存、OFF のまま終了なら消去する
                if (viewModel.HistoryEnabled)
                {
                    historyStore.Save(viewModel.HistoryEntries);
                }
                else
                {
                    historyStore.Delete();
                }
            };

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
