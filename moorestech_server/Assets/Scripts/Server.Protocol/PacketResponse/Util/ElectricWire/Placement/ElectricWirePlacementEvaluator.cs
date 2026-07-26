using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.EnergySystem;
using Server.Protocol.PacketResponse.Util.ConnectTool;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.Placement
{
    /// <summary>
    /// ワイヤー接続の可否を純粋関数として判定する。消費はconnectToolマスタ駆動の複数素材
    /// Judge wire connection eligibility as a pure function; consumption is connectTool-master driven multi-material
    /// </summary>
    public static class ElectricWirePlacementEvaluator
    {
        /// <summary>
        /// distanceはコスト計算専用。接続可否の範囲判定は呼び出し側がIsMutuallyConnectableで行う
        /// distance is for cost only; range judgement is the caller's duty via IsMutuallyConnectable
        /// </summary>
        public static ElectricWirePlacementJudgement EvaluateWireConnection(
            float distance,
            bool alreadyConnected,
            bool anyConnectionFull,
            Guid connectToolGuid,
            IEnumerable<IItemStack> inventoryItems,
            IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            if (alreadyConnected) return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.AlreadyConnected);
            if (anyConnectionFull) return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);

            // インベントリを一度だけ列挙して使い回す
            // Materialize inventory once for reuse across the following checks
            var items = inventoryItems as IReadOnlyCollection<IItemStack> ?? inventoryItems.ToList();

            // connectToolマスタから複数素材の消費量を算出する
            // Calculate multi-material consumption from the connectTool master
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, distance, out var materials))
                return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.NoWireItem);

            // 各素材について、予約分を上乗せした必要数を所持が満たすか確認する
            // For each material, verify held count covers the requirement plus any reservation
            foreach (var material in materials)
            {
                var reserved = SumReserved(material.ItemId);
                if (!HasEnoughItem(material.ItemId, material.Count + reserved))
                    return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.NoWireItem);
            }

            return ElectricWirePlacementJudgement.Success(new ElectricWireConnectionCost(materials));

            #region Internal

            int SumReserved(ItemId itemId)
            {
                // 予約リスト中の同一アイテム数を合計する
                // Sum the reserved amount of the same item in the reservation list
                if (reservedMaterials == null) return 0;
                var reserved = 0;
                foreach (var material in reservedMaterials)
                {
                    if (material.ItemId == itemId) reserved += material.Count;
                }
                return reserved;
            }

            bool HasEnoughItem(ItemId itemId, int required)
            {
                // 対象アイテムの合計所持数が必要数を満たすか確認する
                // Check whether the summed count of the target item meets the requirement
                var total = 0;
                foreach (var itemStack in items)
                {
                    if (itemStack.Id != itemId) continue;
                    total += itemStack.Count;
                    if (required <= total) return true;
                }

                return required <= total;
            }

            #endregion
        }

        public static bool TryCalculateWireCost(Guid connectToolGuid, float distance, out ElectricWireConnectionCost cost)
        {
            cost = ElectricWireConnectionCost.Empty;
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, distance, out var materials)) return false;
            cost = new ElectricWireConnectionCost(materials);
            return true;
        }
    }
}
