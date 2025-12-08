using System;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChain接続のコスト計算を行うユーティリティクラス
    /// Utility class for calculating GearChain connection costs
    /// </summary>
    public static class GearChainCostCalculator
    {
        /// <summary>
        /// 接続に必要なチェーンアイテム数を計算する
        /// Calculate required chain item count for connection
        /// </summary>
        public static int CalculateRequiredChainCount(Vector3Int posA, Vector3Int posB, ItemId chainItemId)
        {
            var distance = Vector3Int.Distance(posA, posB);
            return CalculateRequiredChainCount(distance, chainItemId);
        }

        /// <summary>
        /// 距離に基づいてチェーンアイテム数を計算する
        /// Calculate chain item count based on distance
        /// </summary>
        public static int CalculateRequiredChainCount(float distance, ItemId chainItemId)
        {
            var consumptionPerLength = GetConsumptionPerLength(chainItemId);
            if (consumptionPerLength <= 0) return int.MaxValue;
            return Mathf.CeilToInt(distance / consumptionPerLength);
        }

        /// <summary>
        /// チェーンアイテムのconsumptionPerLengthを取得する
        /// Get consumptionPerLength of chain item
        /// </summary>
        public static float GetConsumptionPerLength(ItemId chainItemId)
        {
            // MasterHolderからgearChainItems設定を取得する
            // Get gearChainItems configuration from MasterHolder
            var gearChainItems = MasterHolder.BlockMaster.Blocks.GearChainItems;
            if (gearChainItems == null || gearChainItems.Length == 0) return 1f;

            foreach (var gearChainItem in gearChainItems)
            {
                var configItemId = MasterHolder.ItemMaster.GetItemId(gearChainItem.ItemGuid);
                if (configItemId == chainItemId)
                {
                    return gearChainItem.ConsumptionPerLength;
                }
            }

            // 見つからなかった場合はデフォルト値を返す
            // Return default value if not found
            return 1f;
        }

        /// <summary>
        /// 指定したアイテムがチェーンアイテムかどうかを確認する
        /// Check if the specified item is a chain item
        /// </summary>
        public static bool IsChainItem(ItemId itemId)
        {
            var gearChainItems = MasterHolder.BlockMaster.Blocks.GearChainItems;
            if (gearChainItems == null || gearChainItems.Length == 0) return false;

            foreach (var gearChainItem in gearChainItems)
            {
                var configItemId = MasterHolder.ItemMaster.GetItemId(gearChainItem.ItemGuid);
                if (configItemId == itemId) return true;
            }

            return false;
        }
    }
}
