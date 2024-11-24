using Client.Game.InGame.Block;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ChestBlockInventory : BlockInventoryBase
    {
        public override void Initialize(BlockGameObject blockGameObject)
        {
            var pos = blockGameObject.BlockPosInfo.OriginalPos;
            ItemMoveInventoryInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, pos);
        }
    }
}