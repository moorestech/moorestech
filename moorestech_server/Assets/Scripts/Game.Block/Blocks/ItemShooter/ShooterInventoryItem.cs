using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.ItemShooter
{
    public class ShooterInventoryItem : IOnBeltConveyorItem
    {
        public ItemId ItemId { get; }

        public ItemInstanceId ItemInstanceId { get; }

        public double RemainingPercent { get; set; }

        public BlockConnectorInfo StartConnector { get; }

        public BlockConnectorInfo GoalConnector { get; set; }

        public float CurrentSpeed { get; set; }

        public ShooterInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, float currentSpeed, BlockConnectorInfo startConnector, BlockConnectorInfo goalConnector)
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
