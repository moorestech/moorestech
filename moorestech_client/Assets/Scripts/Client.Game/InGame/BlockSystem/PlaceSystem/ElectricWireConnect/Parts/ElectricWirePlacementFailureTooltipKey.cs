using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線設置判定の失敗理由をカーソルツールチップの辞書キーへ写像する
    /// Maps an electric wire placement failure reason to a cursor-tooltip dictionary key
    /// </summary>
    public static class ElectricWirePlacementFailureTooltipKey
    {
        public static LocalizationKey ToKey(ElectricWirePlacementFailureReason reason)
        {
            return reason switch
            {
                ElectricWirePlacementFailureReason.OutOfRange => LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRange,
                ElectricWirePlacementFailureReason.AlreadyConnected => LocalizationKeys.Ui.Tooltip.PlaceWireAlreadyConnected,
                ElectricWirePlacementFailureReason.ConnectionLimit => LocalizationKeys.Ui.Tooltip.PlaceWireConnectionLimit,
                ElectricWirePlacementFailureReason.NoWireItem => LocalizationKeys.Ui.Tooltip.PlaceWireNoWireItem,
                ElectricWirePlacementFailureReason.InvalidTarget => LocalizationKeys.Ui.Tooltip.PlaceWireInvalidTarget,
                ElectricWirePlacementFailureReason.PositionOccupied => LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock,
                // 上記以外（切断系・未解放・サーバー側のみの理由）はクライアントの設置判定では発生しないため既定文言へ
                // Everything else (disconnect-side, not-unlocked, server-only reasons) never arises in client placement judgement, so fall back
                _ => LocalizationKeys.Ui.Tooltip.PlaceWireFailed,
            };
        }
    }
}
