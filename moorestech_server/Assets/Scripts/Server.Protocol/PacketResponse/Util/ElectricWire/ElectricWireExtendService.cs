using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// レール式延長（電柱設置＋起点接続＋機械自動接続）を実行する。全検証を設置前に完了し通過時のみ状態変更する
    /// Runs rail-style extend (place pole + wire origin + auto-connect machines); validates all before placing, mutates only on pass
    /// </summary>
    public static class ElectricWireExtendService
    {
        public static ExtendResult Execute(bool hasFromConnector, Vector3Int fromPos, PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack polePlaceInfo, int playerId, int poleInventorySlot, ItemId wireItemId)
        {
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 設置先が既に埋まっていないか確認する
            // Ensure the target position is not already occupied
            if (ServerContext.WorldBlockDatastore.Exists(polePlaceInfo.Position))
                return ExtendResult.Failure(ElectricWirePlacementEvaluator.PositionOccupiedError);

            // 指定スロットのアイテムが電柱ブロックであることを確認する
            // Ensure the item in the given slot resolves to an electric pole block
            var poleItem = inventory.GetItem(poleInventorySlot);
            if (poleItem.Count < 1 || !MasterHolder.BlockMaster.IsBlock(poleItem.Id))
                return ExtendResult.Failure(ElectricWirePlacementEvaluator.NoPoleItemError);

            var blockId = MasterHolder.BlockMaster.GetBlockId(poleItem.Id).GetVerticalOverrideBlockId(polePlaceInfo.VerticalDirection);
            if (MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockParam is not ElectricPoleBlockParam poleParam)
                return ExtendResult.Failure(ElectricWirePlacementEvaluator.InvalidTargetError);

            // 起点接続と機械接続の全ターゲット・電線コストを設置前に組み立てる
            // Build all targets (origin + machines) and their wire cost before placing anything
            var targets = new List<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)>();
            var totalWire = 0;
            IElectricWireConnector fromConnector = null;

            if (hasFromConnector)
            {
                if (!ElectricWireSystemUtil.TryGetWireConnector(fromPos, out fromConnector))
                    return ExtendResult.Failure(ElectricWirePlacementEvaluator.InvalidTargetError);

                var distance = Vector3Int.Distance(fromPos, polePlaceInfo.Position);
                if (distance > Mathf.Min(fromConnector.MaxWireLength, poleParam.MaxWireLength))
                    return ExtendResult.Failure(ElectricWirePlacementEvaluator.TooFarError);
                if (fromConnector.IsWireConnectionFull)
                    return ExtendResult.Failure(ElectricWirePlacementEvaluator.ConnectionLimitError);
                if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(wireItemId, distance, out var fromCost))
                    return ExtendResult.Failure(ElectricWirePlacementEvaluator.NoWireItemError);

                targets.Add((fromConnector.BlockInstanceId, fromCost));
                totalWire += fromCost.Count;
            }

            // 起点接続で1本使う前提で残り本数まで未接続機械を収集する
            // Collect unconnected machines up to remaining capacity, accounting for the origin wire slot
            var machineTargets = ElectricWireAutoConnectTargetCollector.CollectPoleMachineTargets(poleParam, polePlaceInfo.Position, hasFromConnector ? 1 : 0);
            foreach (var machineTarget in machineTargets)
            {
                if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(wireItemId, machineTarget.Distance, out var cost))
                    return ExtendResult.Failure(ElectricWirePlacementEvaluator.NoWireItemError);
                targets.Add((machineTarget.TargetId, cost));
                totalWire += cost.Count;
            }

            // 電柱1個＋電線合計の所持を確認する。電柱と電線が同一アイテムなら合算で判定する
            // Verify holding one pole plus the total wires; when pole and wire share an item, check the combined amount
            var wireNeeded = totalWire + (poleItem.Id == wireItemId ? 1 : 0);
            if (CountItem(inventory, wireItemId) < wireNeeded)
                return ExtendResult.Failure(ElectricWirePlacementEvaluator.NoWireItemError);

            // 検証をすべて通過したのでここから状態を変更する
            // All validation passed; start mutating state from here
            var createParams = polePlaceInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, polePlaceInfo.Position, polePlaceInfo.Direction, createParams, out var placedBlock))
                return ExtendResult.Failure(ElectricWirePlacementEvaluator.PositionOccupiedError);

            var selfConnector = placedBlock.GetComponent<IElectricWireConnector>();
            var connectedConnectors = new List<IElectricWireConnector> { selfConnector };

            foreach (var (targetId, cost) in targets)
            {
                var targetConnector = ServerContext.WorldBlockDatastore.GetBlock(targetId)?.GetComponent<IElectricWireConnector>();
                if (targetConnector == null) continue;
                selfConnector.TryAddWireConnection(targetId, cost);
                targetConnector.TryAddWireConnection(selfConnector.BlockInstanceId, cost);
                connectedConnectors.Add(targetConnector);
            }

            // 電柱を1個、電線を合計分消費してから連結成分を再構築する
            // Consume one pole and the total wires, then rebuild connected components
            inventory.SetItem(poleInventorySlot, inventory.GetItem(poleInventorySlot).SubItem(1));
            ConsumeWire(inventory, wireItemId, totalWire);
            ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(connectedConnectors.ToArray());

            return ExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());
        }

        private static void ConsumeWire(IOpenableInventory inventory, ItemId wireItemId, int amount)
        {
            var remaining = amount;
            for (var i = 0; i < inventory.InventoryItems.Count && remaining > 0; i++)
            {
                var itemStack = inventory.InventoryItems[i];
                if (itemStack.Id != wireItemId) continue;
                var consume = Mathf.Min(itemStack.Count, remaining);
                inventory.SetItem(i, itemStack.SubItem(consume));
                remaining -= consume;
            }
        }

        private static int CountItem(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;
            return total;
        }

        public readonly struct ExtendResult
        {
            public readonly bool IsSuccess;
            public readonly string Error;
            public readonly Vector3Int PlacedPolePos;
            public readonly int PlacedBlockInstanceId;

            private ExtendResult(bool isSuccess, string error, Vector3Int placedPolePos, int placedBlockInstanceId)
            {
                IsSuccess = isSuccess;
                Error = error;
                PlacedPolePos = placedPolePos;
                PlacedBlockInstanceId = placedBlockInstanceId;
            }

            public static ExtendResult Success(Vector3Int placedPolePos, int placedBlockInstanceId)
            {
                return new ExtendResult(true, string.Empty, placedPolePos, placedBlockInstanceId);
            }

            public static ExtendResult Failure(string error)
            {
                return new ExtendResult(false, error, Vector3Int.zero, 0);
            }
        }
    }
}
