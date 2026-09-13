# PasswordStrength

パスワード強度評価ライブラリ。UI や特定アプリケーションに依存しない。

- 依存: .NET 10 標準ライブラリ、Zxcvbn（Zxcvbn.NET, MIT）
- 方針: アプリが生成したパスワードは生成条件から**正確に**エントロピーを計算し、
  ユーザーが自由入力・編集したパスワードだけ zxcvbn で**推定**する。

## 最小コード例

```csharp
using PasswordStrength;

var service = new PasswordStrengthService();

// アプリが 62 文字集合から 20 文字を一様独立に生成した場合（≒ 119.08 bit）
var generated = service.EvaluateGeneratedPassword(new GeneratedPasswordParameters
{
    Length = 20,
    CharacterPoolSize = 62,
});

// 5,872 語の辞書から 6 語（重複可）、区切りは固定文字（≒ 75.12 bit）
var passphrase = service.EvaluateGeneratedPassphrase(new PassphraseParameters
{
    DictionarySize = 5872,
    WordCount = 6,
    AllowDuplicateWords = true,
});

// ユーザーの自由入力（zxcvbn 推定）
var typed = service.EvaluateUserPassword("password123");

Console.WriteLine($"{generated.Score} {generated.ExactEntropyBits:F2} bit");
Console.WriteLine($"{typed.Score} 推定 {typed.EstimatedGuessBits:F2} bit  {typed.Warning}");
```

## 別プロジェクトへ切り出す場合

このフォルダー（`PasswordStrength/`）をそのままコピーするか、`PasswordStrength.csproj` を参照する。
