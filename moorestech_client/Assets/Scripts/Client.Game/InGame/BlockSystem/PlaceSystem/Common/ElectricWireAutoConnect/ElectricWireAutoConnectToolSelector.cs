using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BuildMenuModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// セル1つ分の接続先集合を賄えるelectricWire connectToolをSortPriority順に仮想在庫から選ぶ
    /// Picks the electricWire connectTool covering one cell's targets in SortPriority order against the virtual inventory
    /// </summary>
    public static class ElectricWireAutoConnectToolSelector
    {
        /// <summary>
        /// 全ターゲットを賄えるconnectToolをSortPriority順に選ぶ（サーバーと同じ選定規則）
        /// Picks the connectTool covering all targets in SortPriority order (same rule as the server)
        /// </summary>
        public static bool TrySelect(List<(Vector3Int TargetPos, float Distance)> targets, ElectricWireAutoConnectVirtualInventory virtualInventory, out IReadOnlyList<ConnectToolMaterialCost> selectedMaterials, out int selectedCost)
        {
            selectedMaterials = null;
            selectedCost = 0;

            // 接続先なし・electricWire未設定マスタは自動接続なしで設置可
            // No targets or no configured electricWire connectTool allows placement without auto-connect
            if (targets.Count == 0) return true;
            var electricWireTools = new List<ConnectToolMasterElement>();
            foreach (var element in MasterHolder.ConnectToolMaster.All)
                if (element.ToolType == ConnectToolMasterElement.ToolTypeConst.electricWire) electricWireTools.Add(element);
            if (electricWireTools.Count == 0) return true;
            electricWireTools.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));

            foreach (var element in electricWireTools)
            {
                if (!TrySumCost(element.ConnectToolGuid, targets, out var materials, out var cost)) continue;
                if (!virtualInventory.CanAfford(materials)) continue;

                selectedMaterials = materials;
                selectedCost = cost;
                return true;
            }

            return false;
        }

        // 対象connectToolで全ターゲット分のコストを素材ID別に合算する
        // Sum the cost across all targets per item id for the given connectTool
        private static bool TrySumCost(Guid connectToolGuid, List<(Vector3Int TargetPos, float Distance)> targets, out IReadOnlyList<ConnectToolMaterialCost> materials, out int cost)
        {
            cost = 0;
            var accumulator = new Dictionary<ItemId, int>();
            foreach (var target in targets)
            {
                if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, target.Distance, out var targetCost))
                {
                    materials = null;
                    return false;
                }
                cost += targetCost.TotalCount;
                foreach (var material in targetCost.Materials)
                {
                    accumulator.TryGetValue(material.ItemId, out var current);
                    accumulator[material.ItemId] = current + material.Count;
                }
            }

            var list = new List<ConnectToolMaterialCost>(accumulator.Count);
            foreach (var (itemId, count) in accumulator) list.Add(new ConnectToolMaterialCost(itemId, count));
            materials = list;
            return true;
        }
    }
}
