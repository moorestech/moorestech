using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Constant;
using UnityEngine;

namespace MainGame.Network.Event
{
    public class ReceiveBlockInventoryEvent
    {
        public event Action<BlockInventorySlotUpdateProperties> OnBlockInventorySlotUpdate;
        public event Action<SettingBlockInventoryProperties> OnSettingBlockInventory;

        internal async UniTask InvokeSettingBlock(SettingBlockInventoryProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnSettingBlockInventory?.Invoke(properties);
        }


        internal async UniTask InvokeBlockInventorySlotUpdate(BlockInventorySlotUpdateProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnBlockInventorySlotUpdate?.Invoke(properties);
        }
    }

    public class BlockInventorySlotUpdateProperties
    {
        public readonly int Count;
        public readonly int Id;
        public readonly Vector2Int Position;
        public readonly int Slot;

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
        public readonly int BlockId;
        public readonly List<ItemStack> ItemStacks;

        public SettingBlockInventoryProperties(List<ItemStack> itemStacks, int blockId)
        {
            ItemStacks = itemStacks;
            BlockId = blockId;
        }
    }
}