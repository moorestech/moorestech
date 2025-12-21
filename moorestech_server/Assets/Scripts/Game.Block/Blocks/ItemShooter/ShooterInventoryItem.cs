using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;

namespace Game.Block.Blocks.ItemShooter
{
    public class ShooterInventoryItem : IOnBeltConveyorItem
    {
        public ItemId ItemId { get; }

        public ItemInstanceId ItemInstanceId { get; }

        public double RemainingPercent { get; set; }

        public string SourcePathId { get; }

        public float CurrentSpeed { get; set; }

        public ShooterInventoryItem(ItemId itemId, ItemInstanceId itemInstanceId, float currentSpeed, string sourcePathId)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            CurrentSpeed = currentSpeed;
            SourcePathId = sourcePathId;
            RemainingPercent = 1;
        }
    }
}