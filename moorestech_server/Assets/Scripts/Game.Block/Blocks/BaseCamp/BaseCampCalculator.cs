using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.BaseCamp
{
    /// <summary>
    /// ベースキャンプの進捗計算用ユーティリティクラス
    /// Utility class for base camp progress calculation
    /// </summary>
    public static class BaseCampCalculator
    {
        /// <summary>
        /// 必要なアイテムがすべて納品されたかどうかを確認する
        /// Check if all required items have been delivered
        /// </summary>
        public static bool CalculateIsCompleted(IReadOnlyList<IItemStack> inventoryItems, Dictionary<ItemId, int> requiredItems)
        {
            // 現在のインベントリ内のアイテムを集計
            // Count items currently in inventory
            var currentItems = new Dictionary<ItemId, int>();
            foreach (var item in inventoryItems)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                
                if (!currentItems.ContainsKey(item.Id))
                    currentItems[item.Id] = 0;
                currentItems[item.Id] += item.Count;
            }
            
            // 必要なアイテムがすべて揃っているか確認
            // Check if all required items are present
            foreach (var required in requiredItems)
            {
                if (!currentItems.ContainsKey(required.Key) || currentItems[required.Key] < required.Value)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 納品の進捗率を取得する（0.0～1.0）
        /// Get delivery progress (0.0 to 1.0)
        /// </summary>
        public static float CalculateProgress(IReadOnlyList<IItemStack> inventoryItems, Dictionary<ItemId, int> requiredItems)
        {
            // 現在のインベントリ内のアイテムを集計
            // Count items currently in inventory
            var currentItems = new Dictionary<ItemId, int>();
            foreach (var item in inventoryItems)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                
                if (!currentItems.ContainsKey(item.Id))
                    currentItems[item.Id] = 0;
                currentItems[item.Id] += item.Count;
            }
            
            // 必要な総アイテム数と納品済みアイテム数を計算
            // Calculate total required items and delivered items
            var totalRequired = requiredItems.Sum(r => r.Value);
            var totalDelivered = 0;
            
            foreach (var required in requiredItems)
            {
                if (currentItems.ContainsKey(required.Key))
                {
                    totalDelivered += System.Math.Min(currentItems[required.Key], required.Value);
                }
            }
            
            return totalRequired > 0 ? (float)totalDelivered / totalRequired : 0f;
        }
    }
}