using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class ReceiveBlockInventoryEvent 
    {
        public event Action<BlockInventorySlotUpdateProperties> OnBlockInventorySlotUpdate;
        public event Action<SettingBlockInventoryProperties> OnSettingBlockInventory;

        internal void InvokeSettingBlock(List<ItemStack> items,int blockId)
        {
            InvokeSettingBlockAsync(items,blockId).Forget();
        }
        private async UniTask InvokeSettingBlockAsync(List<ItemStack> items,int blockId)
        {
            await UniTask.SwitchToMainThread();
            OnSettingBlockInventory?.Invoke(new SettingBlockInventoryProperties(items, blockId));
        }
        
        
        

        internal void InvokeBlockInventorySlotUpdate(Vector2Int pos, int slot, int id, int count)
        {
            InvokeBlockInventorySlotUpdateAsync(new BlockInventorySlotUpdateProperties(pos, slot, id, count)).Forget();
        }
        private async UniTask InvokeBlockInventorySlotUpdateAsync(BlockInventorySlotUpdateProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnBlockInventorySlotUpdate?.Invoke(properties);
        }
    }

    public class BlockInventorySlotUpdateProperties
    {
        public readonly Vector2Int Position;
        public readonly int Slot;
        public readonly int Id;
        public readonly int Count;

        public BlockInventorySlotUpdateProperties(Vector2Int position, int slot, int id, int count)
        {
            Position = position;
            Slot = slot;
            Id = id;
            Count = count;
        }
    }

    public class SettingBlockInventoryProperties
    {
        public readonly  List<ItemStack> ItemStacks;
        public readonly int BlockId;

        public SettingBlockInventoryProperties(List<ItemStack> itemStacks, int blockId)
        {
            ItemStacks = itemStacks;
            this.BlockId = blockId;
        }
    }
}