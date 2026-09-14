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

        // 書き出す中身が無い、または書けなかった。待ちは正常に明けているが成果物は1件も出ていない
        // Nothing was there to write, or it could not be written; the wait cleared normally but no artifact came out
        NothingFlushed,

        // 参加者の書き出しが例外で落ちた。何が書けて何が書けていないかは参加者自身にも分かっていない
        // A participant's flush died on an exception; not even the participant knows what was written and what was not
        FlushFailed,
    }
}
