namespace Client.PlaytestReceiver.Http
{
    // 箱の宣言1件。ワイヤに乗るのはPathとBytesで、AbsolutePathは送る側の手元だけで使う
    // One declared file; Path and Bytes go on the wire, AbsolutePath stays on the sending side
    public sealed class PlaytestDeclaredFile
    {
        public readonly string Path;
        public readonly long Bytes;
        public readonly string AbsolutePath;

        public PlaytestDeclaredFile(string path, long bytes, string absolutePath)
        {
            Path = path;
            Bytes = bytes;
            AbsolutePath = absolutePath;
        }
    }
}
