using System.Collections.Generic;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class BlockInventoryUpdateEvent 
    {
        public delegate void BlockInventorySlotUpdate(BlockInventorySlotUpdateProperties properties);
        public delegate void SettingBlockInventory(SettingBlockInventoryProperties properties);
        private event BlockInventorySlotUpdate OnBlockInventorySlotUpdate;
        private event SettingBlockInventory OnSettingBlock;
        public void Subscribe(BlockInventorySlotUpdate onBlockInventorySlot, SettingBlockInventory onSettingBlock)
        {
            OnBlockInventorySlotUpdate += onBlockInventorySlot;
            OnSettingBlock += onSettingBlock;
        }

        public void OnOnSettingBlock(List<ItemStack> items, string uiType, params short[] uiParams)
        {
            OnSettingBlock?.Invoke(new SettingBlockInventoryProperties(items, uiType, uiParams));
        }

        public void OnOnBlockInventorySlotUpdate(Vector2Int pos, int slot, int id, int count)
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

        public SettingBlockInventoryProperties(List<ItemStack> items, string uiType, short[] uiParams)
        {
            this.items = items;
            this.uiType = uiType;
            this.uiParams = uiParams;
        }
    }
}