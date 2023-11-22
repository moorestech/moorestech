using System.Collections.Generic;
using Core.Inventory;
using Core.Item;

namespace Game.Crafting.Interface
{
    /// <summary>
    /// ワンクリックでメインインベントリ内のアイテムをクラフトするサービス
    /// </summary>
    public static class OneClickMainInventoryCraftService
    {
        /// <summary>
        /// TODO 今は旧クラフトシステムのデータを流用しているためこうなっている ワンクリッククラフトで確定したら旧クラフトシステムは消す
        /// </summary>
        /// <param name="inventory"></param>
        /// <param name="craftingConfigData"></param>
        /// <returns></returns>
        public static bool IsCraftable(this IOpenableInventory inventory, CraftingConfigData craftingConfigData)
        {
            //クラフトに必要なアイテムを収集する
            //key itemId value count
            var requiredItems = new Dictionary<int,int>();
            foreach (var itemData in craftingConfigData.CraftItemInfos)
            {
                if (requiredItems.ContainsKey(itemData.ItemStack.Id))
                {
                    requiredItems[itemData.ItemStack.Id] += itemData.ItemStack.Count;
                }
                else
                {
                    requiredItems.Add(itemData.ItemStack.Id, itemData.ItemStack.Count);
                }
            }
            
            //クラフトに必要なアイテムを持っているか確認する
            var checkResult = new Dictionary<int,int>();
            foreach (var itemStack in inventory.Items)
            {
                if (!requiredItems.ContainsKey(itemStack.Id)) continue;
                
                if (checkResult.ContainsKey(itemStack.Id))
                {
                    checkResult[itemStack.Id] += itemStack.Count;
                }
                else
                {
                    checkResult[itemStack.Id] = itemStack.Count;
                }
            }
            
            //必要なアイテムを持っていない場合はクラフトできない
            foreach (var requiredItem in requiredItems)
            {
                if (!checkResult.ContainsKey(requiredItem.Key))
                {
                    return false;
                }
                if (checkResult[requiredItem.Key] < requiredItem.Value)
                {
                    return false;
                }
            }


            return true;
        }


        /// <summary>
        /// クラフトしてアイテムを消費する
        /// </summary>
        public static void SubItem(this IOpenableInventory inventory, CraftingConfigData craftingConfigData)
        {
            //クラフトに必要なアイテムを収集する
            //key itemId value count
            var requiredItems = new Dictionary<int,int>();
            foreach (var itemData in craftingConfigData.CraftItemInfos)
            {
                if (requiredItems.ContainsKey(itemData.ItemStack.Id))
                {
                    requiredItems[itemData.ItemStack.Id] += itemData.ItemStack.Count;
                }
                else
                {
                    requiredItems.Add(itemData.ItemStack.Id, itemData.ItemStack.Count);
                }
            }
            
            //クラフトのために消費する
            for (var i = 0; i < inventory.Items.Count; i++)
            {
                var inventoryItem = inventory.Items[i];
                if (!requiredItems.ContainsKey(inventoryItem.Id)) continue;
                
                var subCount = requiredItems[inventoryItem.Id];
                if (inventoryItem.Count <= subCount)
                {
                    inventory.SetItem(i, inventoryItem.SubItem(inventoryItem.Count));
                    requiredItems[inventoryItem.Id] -= inventoryItem.Count;
                }
                else
                {
                    inventory.SetItem(i, inventoryItem.SubItem(subCount));
                    requiredItems[inventoryItem.Id] -= subCount;
                }
            }
        }
    }
}