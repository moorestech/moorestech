using Game.Block.Interface.Component;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
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
            IReadOnlyList<ConnectToolMaterialCost> reservedMaterials,
            bool isFreePlacement)
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

            // 無料設置は所持を問わずコスト0の接続として記録し、撤去時にも素材を返さない
            // Free placement ignores holdings and records a zero-cost wire, so removal refunds nothing either
            if (isFreePlacement) return ElectricWirePlacementJudgement.Success(CreateFreeRecord(connectToolGuid));

            // 予約分を上乗せした必要数を所持が満たすかは共有の正本へ委ねる
            // Whether the held count covers the requirement plus the reservation is delegated to the shared definition
            if (!ConstructionMaterialAccounting.HasEnough(materials, items, reservedMaterials))
                return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.NoWireItem);

            return ElectricWirePlacementJudgement.Success(new ConnectionLineRecord(connectToolGuid, materials));
        }

        // 無料設置の接続線記録。引き直し用にツール種別は保ち、払った素材は空にする
        // Record for a free-placement line: keeps the tool type for re-drawing, with no paid materials
        public static ConnectionLineRecord CreateFreeRecord(Guid connectToolGuid)
        {
            return new ConnectionLineRecord(connectToolGuid, Array.Empty<ConnectToolMaterialCost>());
        }

        public static bool TryCreateWireRecord(Guid connectToolGuid, float distance, out ConnectionLineRecord record)
        {
            record = default;
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, distance, out var materials)) return false;
            record = new ConnectionLineRecord(connectToolGuid, materials);
            return true;
        }
    }
}
