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

        public SaveWriteCompletion(long generation, ulong tick, string targetPath, bool success)
        {
            Generation = generation;
            Tick = tick;
            TargetPath = targetPath;
            Success = success;
        }
    }
}
