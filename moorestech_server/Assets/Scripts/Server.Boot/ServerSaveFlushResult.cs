namespace Server.Boot
{
    // 終了時のセーブ書き出しがどう終わったか
    // How the shutdown save flush finished
    public enum ServerSaveFlushResult
    {
        Flushed,
        FlushTimedOut,

        // 書き出しに繰り返し失敗して諦めた。待ちは明けているが世界は保存されていない
        // Repeated write failures made the save give up; the wait cleared but the world is not saved
        SaveAbandoned,
    }
}
