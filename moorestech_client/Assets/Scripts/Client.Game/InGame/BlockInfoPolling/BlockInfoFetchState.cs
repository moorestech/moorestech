namespace Client.Game.InGame.BlockInfoPolling
{
    /// <summary>
    ///     表示中ブロックの集約情報の取得状態。Fetched 以外は最新の正常値が無いことを表す
    ///     Fetch state of a displayed block's aggregate; anything but Fetched means no latest valid value
    /// </summary>
    public enum BlockInfoFetchState
    {
        NotFetched,
        Fetched,
        Vanished,
        TransportFailed,
        Unreadable,
        UnsupportedTarget,
    }
}
