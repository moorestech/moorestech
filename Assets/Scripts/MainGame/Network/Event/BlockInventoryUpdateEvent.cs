using System;
using System.Collections.Generic;
using System.Threading;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Network.Event
{
    
    public class BlockInventoryUpdateEvent 
    {
        private SynchronizationContext _mainThread;
        
        public BlockInventoryUpdateEvent()
        {
            //Unityではメインスレッドでしか実行できないのでメインスレッドを保存しておく
            _mainThread = SynchronizationContext.Current;
        }
        
        
        public event Action<BlockInventorySlotUpdateProperties> OnBlockInventorySlotUpdate;
        public event Action<SettingBlockInventoryProperties> OnSettingBlockInventory;

        internal void InvokeSettingBlock(List<ItemStack> items, string uiType,int blockId, params short[] uiParams)
        {
            _mainThread.Post(_ => OnSettingBlockInventory?.Invoke(new SettingBlockInventoryProperties(items, uiType, uiParams, blockId)), null);
        }

        internal void InvokeBlockInventorySlotUpdate(Vector2Int pos, int slot, int id, int count)
        {
            _mainThread.Post(_ => OnBlockInventorySlotUpdate?.Invoke(new BlockInventorySlotUpdateProperties(pos, slot, id, count)), null);
            
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
        public readonly string uiType;
        public readonly short[] uiParams;
        public readonly int BlockId;

        public SettingBlockInventoryProperties(List<ItemStack> itemStacks, string uiType, short[] uiParams, int blockId)
        {
            ItemStacks = itemStacks;
            this.uiType = uiType;
            this.uiParams = uiParams;
            this.BlockId = blockId;
        }
    }
}