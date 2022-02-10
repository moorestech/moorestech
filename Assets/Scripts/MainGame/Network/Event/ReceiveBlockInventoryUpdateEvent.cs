using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Interface.Receive;
using UnityEngine;
using static MainGame.Network.Interface.Receive.IReceiveBlockInventoryUpdateEvent;

namespace MainGame.Network.Event
{
    public class ReceiveBlockInventoryUpdateEvent : IReceiveBlockInventoryUpdateEvent
    {
        private event OnBlockInventorySlotUpdate OnBlockInventorySlotUpdate;
        private event OnSettingBlockInventory OnSettingBlock;
        public void Subscribe(OnBlockInventorySlotUpdate onBlockInventorySlot, OnSettingBlockInventory onSettingBlock)
        {
            OnBlockInventorySlotUpdate += onBlockInventorySlot;
            OnSettingBlock += onSettingBlock;
        }

        public void OnOnSettingBlock(List<ItemStack> items, string uitype, params short[] uiparams)
        {
            OnSettingBlock?.Invoke(items, uitype, uiparams);
        }

        public void OnOnBlockInventorySlotUpdate(Vector2Int pos, int slot, int id, int count)
        {
            OnBlockInventorySlotUpdate?.Invoke(pos, slot, id, count);
        }
    }
}