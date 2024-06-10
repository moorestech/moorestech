using System;
using Core.Item.Interface;

namespace Game.Block.Interface.Event
{
    /// <summary>
    ///     Subscribeだけができるイベントインタフェース
    ///     勝手にInvokeされないように定義している
    /// </summary>
    public interface IBlockOpenableInventoryUpdateEvent
    {
        public void Subscribe(Action<BlockOpenableInventoryUpdateEventProperties> blockInventoryEvent);
    }
    
    public class BlockOpenableInventoryUpdateEventProperties
    {
        public readonly BlockInstanceId BlockInstanceId;
        public readonly IItemStack ItemStack;
        public readonly int Slot;
        
        public BlockOpenableInventoryUpdateEventProperties(BlockInstanceId blockInstanceId, int slot, IItemStack itemStack)
        {
            ItemStack = itemStack;
            Slot = slot;
            BlockInstanceId = blockInstanceId;
        }
    }
}