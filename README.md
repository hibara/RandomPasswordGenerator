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

## ライセンス

MIT License。サードパーティのライセンスは [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
