using System;
using Core.Master;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Tests.Util
{
    public class PlayerInventoryUtil
    {
        public static int GetInInventoryItemCount(ServiceProvider serviceProvider, int playerId, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            return GetInInventoryItemCount(serviceProvider, playerId, itemId);
        }
        
        public static int GetInInventoryItemCount(ServiceProvider serviceProvider, int playerId, ItemId itemId)
        {
            var inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var playerInventoryData = inventoryDataStore.GetInventoryData(playerId);

            var rewardCount = 0;
            // 報酬アイテムがプレイヤーに付与されていることを確認
            // Ensure reward items are granted to the player
            for (var i = 0; i < playerInventoryData.MainOpenableInventory.GetSlotSize(); i++)
            {
                var item = playerInventoryData.MainOpenableInventory.GetItem(i);
                if (item.Id != itemId) continue;
                rewardCount += item.Count;
            }
            
            return rewardCount;
        }
        
        
    }
}