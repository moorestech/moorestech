using Core.Item.Interface;

namespace Game.Block.Blocks.BeltConveyor
{
    public class BeltConveyorInventoryItem
    {
        public readonly int ItemId;
        public readonly ItemInstanceId ItemInstanceId;
        
        public BeltConveyorInventoryItem(int itemId, double remainingTime, ItemInstanceId itemInstanceId)
        {
            ItemId = itemId;
            RemainingTime = remainingTime;
            ItemInstanceId = itemInstanceId;
        }
        
        /// <summary>
        ///     ベルトコンベア内のアイテムがあと何秒で出るかを入れるプロパティ
        /// </summary>
        public double RemainingTime { get; set; }
    }
}