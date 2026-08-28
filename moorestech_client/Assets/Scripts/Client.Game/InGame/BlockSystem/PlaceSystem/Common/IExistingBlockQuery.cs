using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     設置予定セルへ既存ブロックが重なるかを問い合わせる窓口
    ///     The query port asking whether an existing block overlaps a planned placement cell
    /// </summary>
    public interface IExistingBlockQuery
    {
        bool IsOverlapping(PlaceInfo placeInfo);
    }
}
