using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor;

namespace Game.Block.Blocks.ItemShooter
{
    public class ShooterInventoryItem : IOnBeltConveyorItem
    {
        public int ItemId { get; }
        
        public ItemInstanceId ItemInstanceId { get; }
        
        public float RemainingPercent { get; set; }
        
        public float CurrentSpeed { get; set; }
        
        public ShooterInventoryItem(int itemId, ItemInstanceId itemInstanceId, float currentSpeed)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            CurrentSpeed = currentSpeed;
            RemainingPercent = 1;
        }
    }
}