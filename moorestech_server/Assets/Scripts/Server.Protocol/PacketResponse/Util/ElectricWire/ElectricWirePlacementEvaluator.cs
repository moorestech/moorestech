using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// ワイヤー接続の可否を純粋関数として判定する。距離・既存接続状態・所持アイテムのみから結果を導く
    /// Judge wire connection eligibility as a pure function, using only distance, connection state and held items
    /// </summary>
    public static class ElectricWirePlacementEvaluator
    {
        public const string TooFarError = "TooFar";
        public const string AlreadyConnectedError = "AlreadyConnected";
        public const string ConnectionLimitError = "ConnectionLimit";
        public const string NoWireItemError = "NoWireItem";
        public const string NoPoleItemError = "NoPoleItem";
        public const string InvalidTargetError = "InvalidTarget";
        public const string PositionOccupiedError = "PositionOccupied";

        public static ElectricWirePlacementJudgement EvaluateWireConnection(
            float distance,
            float fromMaxWireLength,
            float toMaxWireLength,
            bool alreadyConnected,
            bool anyConnectionFull,
            ItemId wireItemId,
            IEnumerable<IItemStack> inventoryItems,
            ItemId poleItemId)
        {
            // 距離・既存接続・接続数上限を先に確認する
            // Check distance, existing connection and capacity first
            var maxDistance = Mathf.Min(fromMaxWireLength, toMaxWireLength);
            if (distance > maxDistance) return ElectricWirePlacementJudgement.Failure(TooFarError);
            if (alreadyConnected) return ElectricWirePlacementJudgement.Failure(AlreadyConnectedError);
            if (anyConnectionFull) return ElectricWirePlacementJudgement.Failure(ConnectionLimitError);

            // インベントリを一度だけ列挙して使い回す
            // Materialize inventory once for reuse across the following checks
            var items = inventoryItems as IReadOnlyCollection<IItemStack> ?? inventoryItems.ToList();

            // ポールアイテムが指定されている場合は所持を確認する
            // When a pole item is specified, ensure at least one is held
            if (poleItemId != ItemMaster.EmptyItemId && !HasEnoughItem(items, poleItemId, 1))
                return ElectricWirePlacementJudgement.Failure(NoPoleItemError);

            // ワイヤーコストを算出し、所持数が足りているか確認する
            // Calculate the wire cost and verify inventory covers it
            if (!TryCalculateWireCost(wireItemId, distance, out var cost) || !HasEnoughItem(items, cost.ItemId, cost.Count))
                return ElectricWirePlacementJudgement.Failure(NoWireItemError);

            return ElectricWirePlacementJudgement.Success(cost);
        }

        public static bool TryCalculateWireCost(ItemId wireItemId, float distance, out ElectricWireConnectionCost cost)
        {
            cost = default;

            // 設定が無ければ算出できない
            // Cannot calculate without configuration
            var electricWireItems = MasterHolder.BlockMaster.Blocks.ElectricWireItems;
            if (electricWireItems.Length == 0) return false;

            // 指定されたアイテムが設定に含まれているか確認する
            // Check if the specified item is included in the configuration
            ElectricWireItemsElement matchedWireItem = null;
            foreach (var electricWireItem in electricWireItems)
            {
                var configItemId = MasterHolder.ItemMaster.GetItemId(electricWireItem.ItemGuid);
                if (configItemId != wireItemId) continue;
                matchedWireItem = electricWireItem;
                break;
            }

            if (matchedWireItem == null) return false;

            var required = Mathf.CeilToInt(distance / matchedWireItem.ConsumptionPerLength);
            cost = new ElectricWireConnectionCost(wireItemId, required);
            return true;
        }

        private static bool HasEnoughItem(IEnumerable<IItemStack> items, ItemId itemId, int required)
        {
            // 対象アイテムの合計所持数が必要数を満たすか確認する
            // Check whether the summed count of the target item meets the requirement
            var total = 0;
            foreach (var itemStack in items)
            {
                if (itemStack.Id != itemId) continue;
                total += itemStack.Count;
                if (total >= required) return true;
            }

            return total >= required;
        }
    }
}
