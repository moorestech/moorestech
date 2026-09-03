using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Server.Protocol.PacketResponse.Util.ConnectTool;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 所持スタック列からitemId別の所持数を集計する
    /// Tallies held counts per itemId from a sequence of item stacks
    /// 集計そのものはサーバーと共有の唯一の供給点へ委ねる
    /// The tally itself is delegated to the single supply point shared with the server
    /// </summary>
    public static class ConstructionMaterialHeldCounts
    {
        public static Dictionary<ItemId, int> Tally(IEnumerable<IItemStack> inventoryItems)
        {
            return ConnectToolMaterialConsumer.TallyHeld(inventoryItems);
        }
    }
}
