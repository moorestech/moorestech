using System;
using System.Collections.Generic;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Model.Network.Event
{
    
    public class BlockInventoryUpdateEvent 
    {
        public event Action<BlockInventorySlotUpdateProperties> OnBlockInventorySlotUpdate;
        public event Action<SettingBlockInventoryProperties> OnSettingBlock;

        internal void InvokeSettingBlock(List<ItemStack> items, string uiType,int blockId, params short[] uiParams)
        {
            OnSettingBlock?.Invoke(new SettingBlockInventoryProperties(items, uiType, uiParams, blockId));
        }

        internal void InvokeBlockInventorySlotUpdate(Vector2Int pos, int slot, int id, int count)
        {
            OnBlockInventorySlotUpdate?.Invoke(new BlockInventorySlotUpdateProperties(pos, slot, id, count));
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
        public readonly  List<ItemStack> items;
        public readonly string uiType;
        public readonly short[] uiParams;
        public readonly int blockId;

        public SettingBlockInventoryProperties(List<ItemStack> items, string uiType, short[] uiParams, int blockId)
        {
            this.items = items;
            this.uiType = uiType;
            this.uiParams = uiParams;
            this.blockId = blockId;
        }
    }
}