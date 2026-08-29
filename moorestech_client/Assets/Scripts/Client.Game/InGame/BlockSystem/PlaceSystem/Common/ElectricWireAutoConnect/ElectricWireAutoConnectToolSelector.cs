using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.BuildMenuModule;
using Server.Protocol.PacketResponse.Util.ConnectTool;
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
        /// selectedMaterialsは選ばれたツールの素材。どれも賄えなかったときは最優先ツールの必要素材が入り、不足行の算出に使える
        /// selectedMaterials holds the picked tool's materials; when none is affordable it holds the top-priority tool's requirement, so the shortage lines can be derived from it
        /// </summary>
        public static bool TrySelect(List<(Vector3Int TargetPos, float Distance)> targets, ElectricWireAutoConnectVirtualInventory virtualInventory, IGameUnlockStateData gameUnlockStateData, out IReadOnlyList<ConnectToolMaterialCost> selectedMaterials, out int selectedCost)
        {
            selectedMaterials = null;
            selectedCost = 0;

            // 接続先なし・electricWire未設定マスタは自動接続なしで設置可
            // No targets or no configured electricWire connectTool allows placement without auto-connect
            if (targets.Count == 0) return true;

            // 解放済みフィルタと並び順はサーバーと同一実装を呼んで共有する（手写しすると規則がずれてプレビューと実接続が食い違う）
            // Share the server's own implementation for the unlocked filter and ordering (a hand-copy drifts and desyncs preview from reality)
            var electricWireTools = ConnectToolSelector
                .UnlockedByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, gameUnlockStateData)
                .ToList();

            // 解放済みが0件なら自動接続なしで設置可（サーバーのunlockedTools.Count == 0分岐と一致）
            // With zero unlocked tools, allow placement without auto-connect (matches the server's unlockedTools.Count == 0 branch)
            if (electricWireTools.Count == 0) return true;

            // 最優先で算出できたツールの必要素材を控えておき、どれも賄えなかったときの不足表示に使う
            // Remember the top-priority tool whose cost is computable, to describe the shortage when nothing is affordable
            IReadOnlyList<ConnectToolMaterialCost> preferredMaterials = null;
            foreach (var element in electricWireTools)
            {
                if (!TrySumCost(element.ConnectToolGuid, out var materials, out var cost)) continue;
                preferredMaterials ??= materials;
                if (!virtualInventory.CanAfford(materials)) continue;

                selectedMaterials = materials;
                selectedCost = cost;
                return true;
            }

            selectedMaterials = preferredMaterials;
            return false;

            #region Internal

            // 対象connectToolで全ターゲット分のコストを素材ID別に合算する
            // Sum the cost across all targets per item id for the given connectTool
            bool TrySumCost(Guid connectToolGuid, out IReadOnlyList<ConnectToolMaterialCost> materials, out int cost)
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

            #endregion
        }
    }
}
