namespace Client.Game.InGame.BlockInfoPolling
{
    /// <summary>
    ///     表示中ブロックの集約情報の取得状態
    ///     Fetch state of a displayed block's aggregate
    /// </summary>
    public enum BlockInfoFetchState
    {
        NotFetched,
        Fetched,
        Vanished,
    }
}
