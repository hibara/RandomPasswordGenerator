# Random Password Generator

macOS / Windows / Linux で動くパスワード生成アプリです。.NET 10 + [Avalonia UI](https://avaloniaui.net/) + [ShadUI](https://github.com/accntech/shad-ui) で作られています。

## 機能

- **ランダムなパスワード**: 文字数（1〜64）、大文字・小文字・数字・記号、紛らわしい文字の除外
- **覚えやすいパスワード**: 日本語ローマ字の単語（5,872 語）を並べたパスフレーズ。単語数、区切り（ハイフン、スペース、ピリオド、コンマ、アンダースコア、ランダムな数字、ランダムな数字と記号）、大文字・小文字
- **暗証番号**: 数字のみ（3〜16 桁）
- **強度メーター**: アプリが生成したパスワードは生成条件から正確にエントロピーを計算し、自由入力は zxcvbn で推定（`PasswordStrength` ライブラリ）
- **履歴**: コピーしたパスワードを指定件数だけ保持（ON/OFF 可）
- 日本語 / 英語の UI（OS の言語に従う。`--lang=en` / `--lang=ja` で固定可）

すべて暗号学的に安全な乱数（`RandomNumberGenerator`）で生成します。

## ビルド

```
dotnet build
dotnet run
dotnet test
```

.NET 10 SDK が必要です。

## 構成

| パス | 内容 |
| --- | --- |
| `RandomPasswordGenerator.csproj` | アプリ本体（Avalonia） |
| `PasswordStrength/` | パスワード強度評価ライブラリ（UI 非依存、他アプリから再利用可） |
| `Tests/` | 単体テスト（xUnit） |
| `dic/` | パスフレーズ用の単語辞書と、その作成手順 |

## DLL ハイジャック対策（Windows）

Windows 版は単一 EXE で配布しています。EXE と同じフォルダーに置かれた偽の DLL（`bcrypt.dll` など）が Windows 本来の DLL より先に読み込まれる「DLL ハイジャック」を防ぐため、起動時に次の対策を行います（`Services/WindowsDllSearchGuard.cs`）。

1. `SetDllDirectory("")` と `SetDefaultDllDirectories` で、ネイティブコードからの DLL 検索を System32 に限定する（EXE のフォルダー・カレントフォルダー・PATH を検索順序から外す）。この設定に失敗したときは起動しない
2. .NET の P/Invoke が参照する DLL は System32 だけから読み込む（アプリ同梱のネイティブ DLL は展開先のフルパスで読み込み、どちらにも無い名前は拒否する）。乱数生成の土台になる `bcrypt.dll` などの暗号系 DLL は、起動直後に System32 の本物を読み込んで固定する
3. EXE と同じフォルダーに DLL ファイルがあれば、警告を表示して起動しない（このアプリは EXE の隣に DLL を必要としません）。フォルダーを調べられなかったときも起動しない

ZIP 版を使うときは、EXE を DLL のない専用フォルダーに置いてください。インストーラー版は Program Files にインストールされるため、この問題の影響を受けません。対策の再検証には `Tests/DllHijackCheck/Invoke-DllHijackCheck.ps1` を使えます（publish した単一 EXE の隣におとり DLL を並べて起動し、固有マーカー付きの拒否ダイアログを観測できれば合格）。

## ライセンス

MIT License。サードパーティのライセンスは [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
