using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// ワイヤー設置失敗理由をプレビュー表示用の文言へ変換する
    /// Convert wire placement failure reasons into preview label text
    /// </summary>
    public static class ElectricWirePlacementFailureText
    {
        public static string ToText(ElectricWirePlacementFailureReason reason)
        {
            return reason switch
            {
                ElectricWirePlacementFailureReason.None => string.Empty,
                ElectricWirePlacementFailureReason.OutOfRange => "接続範囲外です",
                ElectricWirePlacementFailureReason.AlreadyConnected => "接続済みです",
                ElectricWirePlacementFailureReason.ConnectionLimit => "接続上限です",
                ElectricWirePlacementFailureReason.NoWireItem => "電線が足りません",
                ElectricWirePlacementFailureReason.NoPoleItem => "電柱が足りません",
                ElectricWirePlacementFailureReason.InvalidTarget => "接続できない対象です",
                ElectricWirePlacementFailureReason.PositionOccupied => "設置位置が埋まっています",
                ElectricWirePlacementFailureReason.InventoryFull => "インベントリがいっぱいです",
                ElectricWirePlacementFailureReason.NotConnected => "接続されていません",
                ElectricWirePlacementFailureReason.NotUnlocked => "未解放です",
                ElectricWirePlacementFailureReason.InsufficientItems => "素材が足りません",
                _ => "設置できません",
            };
        }
    }
}
