namespace Server.Boot
{
    // 終了時のセーブ書き出しがどう終わったか
    // How the shutdown save flush finished
    public enum ServerSaveFlushResult
    {
        Flushed,
        FlushTimedOut,
    }
}
