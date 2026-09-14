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

        // 書き出す中身が1件も無かった。待ちは正常に明けており、書けなかったのではなく書くものが無い
        // There was nothing at all to write; the wait cleared normally and this is an empty flush, never a failed one
        NothingFlushed,

        // 参加者の書き出しが失敗した（例外で落ちた、または握った失敗で書けなかった）。何が書けたかは参加者自身にも分かっていない
        // A participant's flush failed, either by throwing or by a caught failure that left it unwritten; not even the participant knows what got out
        FlushFailed,
    }
}
