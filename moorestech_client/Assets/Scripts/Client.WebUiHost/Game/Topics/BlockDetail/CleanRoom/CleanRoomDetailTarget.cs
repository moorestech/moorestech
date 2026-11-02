namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     開いているブロックの部屋欄をどの口で組むか。判定は BlockNetworkInfoCache.Track の switch だけが行う
    ///     Which entry point builds the open block's room section; only the switch in BlockNetworkInfoCache.Track decides it
    /// </summary>
    public enum CleanRoomDetailTarget
    {
        None,
        Machine,
        AirFilter,
    }
}
