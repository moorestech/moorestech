using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using UnityEngine;

namespace MainGame.GameLogic.Inventory
{
    //TODO blockInventoryを開いたり、UIを更新する
    public class BlockInventoryDataCache
    {
        public BlockInventoryDataCache(ReceiveBlockInventoryUpdateEvent blockInventory)
        {
            blockInventory.Subscribe(OnBlockInventorySlotUpdate,OnSettingBlockInventory);
        }

        private void OnBlockInventorySlotUpdate(Vector2Int pos,int slot,int id,int count)
        {
            //TODO スロットのアップデートを伝える_blockInventoryView.OnInventoryUpdateInvoke(slot,id,count);
        }

        private void OnSettingBlockInventory(List<ItemStack> items,string uiType,params short[] uiParams)
        {
            //TODO UIを開いて更新する
            //UIを開く
            //_blockInventoryView.OnSettingInventoryInvoke(uiType,uiParams);
            //UIを更新する
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                //_blockInventoryView.OnInventoryUpdateInvoke(i,item.ID,item.Count);
            }
        }
        
    }
}