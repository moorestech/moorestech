using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.ItemShooter
{
    public class ShooterInventoryItem : IOnBeltConveyorItem
    {
        public ItemId ItemId { get; }

        public ItemInstanceId ItemInstanceId { get; }

        public double RemainingPercent { get; set; }

        public IBlockConnector StartConnector { get; }

        public IBlockConnector GoalConnector { get; set; }

        public float CurrentSpeed { get; set; }

        public ShooterInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, float currentSpeed, IBlockConnector startConnector, IBlockConnector goalConnector)
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