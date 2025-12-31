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

        public double RemainingPercent { get; set; }

        public BlockConnectInfoElement StartConnector { get; }

        public BlockConnectInfoElement GoalConnector { get; set; }

        public float CurrentSpeed { get; set; }

        public ShooterInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, float currentSpeed, BlockConnectInfoElement startConnector, BlockConnectInfoElement goalConnector)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            CurrentSpeed = currentSpeed;
            StartConnector = startConnector;
            GoalConnector = goalConnector;
            RemainingPercent = 1;
        }
    }
}