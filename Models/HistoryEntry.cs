namespace RandomPasswordGenerator.Models;

/// <summary>クリップボードにコピーしたパスワードの履歴 1 件。メモリ上にのみ保持し、ファイルには保存しない。</summary>
public sealed record HistoryEntry(string Text, GeneratorMode Kind, DateTime CopiedAt);
