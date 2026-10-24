using System;
using Client.Game.InGame.BlockInfoPolling;

namespace Client.WebUiHost.Game.Topics.BlockDetail.FetchState
{
    /// <summary>
    ///     照会欄の fetchState キーへ載せる wire 名。取得状態から文字列を作るのはここだけ
    ///     Wire names for the query sections' fetchState key; the only place turning a fetch state into a string
    /// </summary>
    public static class BlockInfoFetchStateWireName
    {
        public const string Fetched = "fetched";

        // 取得できていない状態の wire 名。Fetched はドメインの値と一緒に各 DTO の factory が載せるので、ここへ来たら呼び出しの誤り
        // Wire name of a not-fetched state; Fetched rides with domain values through each DTO factory, so reaching here with it is a caller bug
        public static string ToUnavailableWireName(BlockInfoFetchState state)
        {
            return state switch
            {
                BlockInfoFetchState.NotFetched => "notFetched",
                BlockInfoFetchState.TransportFailed => "transportFailed",
                BlockInfoFetchState.Unreadable => "unreadable",
                BlockInfoFetchState.UnsupportedTarget => "unsupportedTarget",
                BlockInfoFetchState.Vanished => "vanished",
                BlockInfoFetchState.Fetched => throw new ArgumentException("取得済みは取得できていない状態ではない / Fetched is not an unavailable state", nameof(state)),
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "未対応の取得状態 / Unsupported fetch state"),
            };
        }
    }
}
