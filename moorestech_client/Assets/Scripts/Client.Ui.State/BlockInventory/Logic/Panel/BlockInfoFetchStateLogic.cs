using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Panel
{
    // 詳細を取得できていない状態（Web の BlockInfoUnavailableData.fetchState）
    // States where the detail could not be fetched (Web BlockInfoUnavailableData.fetchState)
    public enum BlockInfoUnavailableState
    {
        NotFetched,
        TransportFailed,
        Unreadable,
        UnsupportedTarget,
        Vanished,
    }

    /// <summary>
    /// 取得できていない状態ごとの文言キー。欄の名前は {target} へ差し込む（Web blockInfoFetchStateLogic.ts の写し）
    /// The message key per unavailable state; the section name goes into {target} (port of Web blockInfoFetchStateLogic.ts)
    /// </summary>
    public static class BlockInfoFetchStateLogic
    {
        public static LocalizationKey UnavailableKey(BlockInfoUnavailableState state)
        {
            return state switch
            {
                BlockInfoUnavailableState.NotFetched => LocalizationKeys.Ui.BlockInventory.BlockInfoNotFetched,
                BlockInfoUnavailableState.TransportFailed => LocalizationKeys.Ui.BlockInventory.BlockInfoTransportFailed,
                BlockInfoUnavailableState.Unreadable => LocalizationKeys.Ui.BlockInventory.BlockInfoUnreadable,
                BlockInfoUnavailableState.UnsupportedTarget => LocalizationKeys.Ui.BlockInventory.BlockInfoUnsupportedTarget,
                BlockInfoUnavailableState.Vanished => LocalizationKeys.Ui.BlockInventory.BlockInfoVanished,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}
