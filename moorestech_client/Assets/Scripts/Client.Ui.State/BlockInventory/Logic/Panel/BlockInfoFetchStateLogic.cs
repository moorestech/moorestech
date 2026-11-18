using System;
using Client.Game.InGame.BlockInfoPolling;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Panel
{
    /// <summary>
    /// 取得できていない状態ごとの文言キー。欄の名前は {target} へ差し込む（Web blockInfoFetchStateLogic.ts の写し）
    /// The message key per unavailable state; the section name goes into {target} (port of Web blockInfoFetchStateLogic.ts)
    /// </summary>
    public static class BlockInfoFetchStateLogic
    {
        // Fetched は取得できているので false を返す。未知の値は throw で止める
        // Fetched means the detail is available, so return false; unknown values throw
        public static bool TryGetUnavailableKey(BlockInfoFetchState state, out LocalizationKey key)
        {
            key = state switch
            {
                BlockInfoFetchState.Fetched => default,
                BlockInfoFetchState.NotFetched => LocalizationKeys.Ui.BlockInventory.BlockInfoNotFetched,
                BlockInfoFetchState.TransportFailed => LocalizationKeys.Ui.BlockInventory.BlockInfoTransportFailed,
                BlockInfoFetchState.Unreadable => LocalizationKeys.Ui.BlockInventory.BlockInfoUnreadable,
                BlockInfoFetchState.UnsupportedTarget => LocalizationKeys.Ui.BlockInventory.BlockInfoUnsupportedTarget,
                BlockInfoFetchState.Vanished => LocalizationKeys.Ui.BlockInventory.BlockInfoVanished,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
            return state != BlockInfoFetchState.Fetched;
        }
    }
}
