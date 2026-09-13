namespace RandomPasswordGenerator.Models;

/// <summary>
/// クリップボードにコピーしたパスワードの履歴 1 件。
/// 履歴が ON のままアプリを終了したときだけ <see cref="Services.HistoryStore"/> がファイルへ保存し、
/// OFF のまま終了したときはファイルを消す。
/// </summary>
public sealed record HistoryEntry(string Text, GeneratorMode Kind, DateTime CopiedAt);
