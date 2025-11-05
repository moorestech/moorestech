using System;
using Core.Item.Interface;

namespace Game.Train.Event
{
    /// <summary>
    /// 列車インベントリ更新イベントのデータ
    /// Data payload for train inventory updates.
    /// </summary>
    public class TrainInventoryUpdateEventProperties
    {
        public Guid TrainCarId { get; }
        public int Slot { get; }
        public IItemStack ItemStack { get; }
        
        public TrainInventoryUpdateEventProperties(Guid trainCarId, int slot, IItemStack itemStack)
        {
            TrainCarId = trainCarId;
            Slot = slot;
            ItemStack = itemStack;
        }
    }
}