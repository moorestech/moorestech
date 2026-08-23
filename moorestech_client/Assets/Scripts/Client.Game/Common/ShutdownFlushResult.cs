namespace Client.Game.Common
{
    // 終了時の書き出し待ちがどう終わったか
    // How the shutdown flush wait finished
    public enum ShutdownFlushResult
    {
        Flushed,
        FlushTimedOut,
        AlreadyShutdown,
    }
}
