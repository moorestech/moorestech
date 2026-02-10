using Core.Item.Interface;
using Game.Train.Unit;

namespace Game.Train.Event
{
    /// <summary>
    /// 列車インベントリ更新イベントのデータ
    /// Data payload for train inventory updates.
    /// </summary>
    public class TrainInventoryUpdateEventProperties
    {
        public TrainCarInstanceId TrainCarInstanceId { get; }
        public int Slot { get; }
        public IItemStack ItemStack { get; }
        
        public TrainInventoryUpdateEventProperties(TrainCarInstanceId trainCarInstanceId, int slot, IItemStack itemStack)
        {
            TrainCarInstanceId = trainCarInstanceId;
            Slot = slot;
            ItemStack = itemStack;
        }
    }
}
