using System;
using Core.Item.Interface;
using UniRx;

namespace Game.Block.Interface.Event
{
    /// <summary>
    ///     Subscribeだけができるイベントインタフェース
    ///     勝手にInvokeされないように定義している
    /// </summary>
    public interface IBlockOpenableInventoryUpdateEvent
    {
        public IObservable<BlockOpenableInventoryUpdateEventProperties> OnInventoryUpdated { get; }
        public IDisposable Subscribe(Action<BlockOpenableInventoryUpdateEventProperties> blockInventoryEvent);
        public void OnInventoryUpdateInvoke(BlockOpenableInventoryUpdateEventProperties properties);
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
