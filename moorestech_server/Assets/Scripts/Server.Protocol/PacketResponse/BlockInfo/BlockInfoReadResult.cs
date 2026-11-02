namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     表示中ブロックの照会応答を読んだ結果。消失だけが問い合わせを止め、読めない応答と非対応の対象は再試行で回復を待つ
    ///     Result of reading a displayed block's query response; only a vanished block stops polling, while unreadable responses and unsupported targets are retried
    /// </summary>
    public enum BlockInfoReadResult
    {
        Success,
        BlockNotFound,
        Unreadable,
        UnsupportedTarget,
    }
}
