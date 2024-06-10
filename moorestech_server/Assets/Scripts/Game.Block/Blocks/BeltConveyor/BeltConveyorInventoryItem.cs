using Core.Item.Interface;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public float RemainingPercent { get; }
        public int ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
    }
    
    public class BeltConveyorInventoryItem : IOnBeltConveyorItem
    {
        public  int ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public float RemainingPercent => (float)(RemainingTime / _timeOfItemEnterToExit);
        
        /// <summary>
        ///     ベルトコンベア内のアイテムがあと何秒で出るかを入れるプロパティ
        /// </summary>
        public double RemainingTime { get; set; }
        
        private readonly double _timeOfItemEnterToExit;
        
        public BeltConveyorInventoryItem(int itemId, double remainingTime, ItemInstanceId itemInstanceId, double timeOfItemEnterToExit)
        {
            ItemId = itemId;
            RemainingTime = remainingTime;
            ItemInstanceId = itemInstanceId;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
        }
    }
}