namespace RandomPasswordGenerator.Models;

/// <summary>生成方式。画面上部のタブに対応する。</summary>
public enum GeneratorMode
{
    /// <summary>ランダムなパスワード。</summary>
    Random,

    /// <summary>覚えやすいパスワード（日本語ローマ字の単語を並べたパスフレーズ）。</summary>
    Passphrase,

    /// <summary>暗証番号（数字のみ）。</summary>
    Pin,

    /// <summary>コピー履歴（生成は行わない）。</summary>
    History,
}
