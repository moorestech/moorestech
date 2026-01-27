using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Blocks.ItemShooter
{
    public class ShooterInventoryItem : IOnBeltConveyorItem
    {
        public ItemId ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public uint RemainingTicks { get; set; }
        public uint TotalTicks { get; }
        public BlockConnectInfoElement StartConnector { get; }
        public BlockConnectInfoElement GoalConnector { get; set; }

        public ShooterInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, uint totalTicks, BlockConnectInfoElement startConnector, BlockConnectInfoElement goalConnector)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            TotalTicks = totalTicks;
            RemainingTicks = totalTicks;
            StartConnector = startConnector;
            GoalConnector = goalConnector;
        }
    }
}