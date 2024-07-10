using Core.Item.Interface;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public double RemainingPercent { get; }
        public int ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
    }
    
    public class BeltConveyorInventoryItem : IOnBeltConveyorItem
    {
        public int ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        
        /// <summary>
        ///     ベルトコンベア内のアイテムが出るまで残り何パーセントか
        /// </summary>
        public double RemainingPercent { get; set; }
        
        public BeltConveyorInventoryItem(int itemId, ItemInstanceId itemInstanceId)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            RemainingPercent = 1;
        }
    }
}