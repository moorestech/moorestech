namespace Client.Game.Common
{
    // 終了時の書き出し待ちがどう終わったか
    // How the shutdown flush wait finished
    public enum ShutdownFlushResult
    {
        Flushed,
        FlushTimedOut,

        // 書き出しに繰り返し失敗して諦めた。待ちは明けているが世界は保存されていない
        // Repeated write failures made the save give up; the wait cleared but the world is not saved
        SaveAbandoned,
        AlreadyShutdown,
    }
}
