namespace Game.SaveLoad.Writer
{
    // 書き出し1件の結果。要求側がtickスレッドで受け取り、完了か再実行かを決める
    // The result of one write; the requester consumes it on the tick thread to decide completion or retry
    public sealed class SaveWriteCompletion
    {
        public long Generation { get; }
        public ulong Tick { get; }
        public string TargetPath { get; }
        public bool Success { get; }

        private SaveWriteCompletion(long generation, ulong tick, string targetPath, bool success)
        {
            Generation = generation;
            Tick = tick;
            TargetPath = targetPath;
            Success = success;
        }

        // プレイヤーのセーブは要求番号で突き合わせる
        // A player save is matched by its generation
        public static SaveWriteCompletion ForPlayerSave(long generation, ulong tick, string targetPath, bool success)
        {
            return new SaveWriteCompletion(generation, tick, targetPath, success);
        }

        // スナップショットは取り込みtickで突き合わせるため要求番号を持たない
        // A snapshot is matched by its captured tick, so it carries no generation
        public static SaveWriteCompletion ForSnapshot(ulong tick, string targetPath, bool success)
        {
            return new SaveWriteCompletion(0, tick, targetPath, success);
        }
    }
}
