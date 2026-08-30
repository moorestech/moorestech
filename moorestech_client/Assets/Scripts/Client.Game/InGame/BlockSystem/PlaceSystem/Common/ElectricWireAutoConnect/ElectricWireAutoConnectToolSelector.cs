using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
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
        /// selectedMaterialsは選ばれたツールの素材で成功時のみ有効。shortagesは失敗時のみ非空で、表示専用の不足素材を運ぶ
        /// selectedMaterials holds the picked tool's materials and is valid on success only; shortages is non-empty on failure only and carries the display-side shortage
        /// </summary>
        public static bool TrySelect(List<(Vector3Int TargetPos, float Distance)> targets, ElectricWireAutoConnectVirtualInventory virtualInventory, IGameUnlockStateData gameUnlockStateData, out IReadOnlyList<ConnectToolMaterialCost> selectedMaterials, out int selectedCost, out IReadOnlyList<ConstructionMaterialShortage> shortages)
        {
            selectedMaterials = null;
            selectedCost = 0;
            shortages = Array.Empty<ConstructionMaterialShortage>();

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

            // どのツールも賄えなかったときに出す不足は、最初にコスト算出できたツールのものを使う
            // When no tool is affordable, the shortage shown is the one from the first tool whose cost could be computed
            IReadOnlyList<ConstructionMaterialShortage> firstShortages = null;
            foreach (var element in electricWireTools)
            {
                if (!TrySumCost(element.ConnectToolGuid, out var materials, out var cost)) continue;
                if (!virtualInventory.CanAfford(materials))
                {
                    firstShortages ??= virtualInventory.CalculateShortages(materials);
                    continue;
                }

                selectedMaterials = materials;
                selectedCost = cost;
                return true;
            }

            // 1件もコスト算出できなかったときは空のまま返し、呼び出し元が汎用文言へ落とす
            // With no computable cost at all the shortage stays empty and the caller falls back to the generic wording
            shortages = firstShortages ?? Array.Empty<ConstructionMaterialShortage>();
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
