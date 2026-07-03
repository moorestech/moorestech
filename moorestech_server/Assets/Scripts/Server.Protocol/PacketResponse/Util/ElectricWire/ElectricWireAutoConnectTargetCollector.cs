using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// 設置位置の周辺から自動接続対象の候補を収集する。電柱設置と機械設置で選定ルールが異なる
    /// Collects auto-connect target candidates around a placement position; rules differ for pole vs machine
    /// </summary>
    public static class ElectricWireAutoConnectTargetCollector
    {
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleTargets(ElectricPoleBlockParam ownParam, Vector3Int position)
        {
            var results = new List<(BlockInstanceId, IElectricWireConnector, float)>();
            var usedCount = 0;

            // ①範囲内で接続可能な最寄り電柱1本
            // Nearest connectable pole within pole range
            var nearestPole = CollectConnectors(ElectricConnectionRangeService.EnumeratePoleRange(position, ownParam))
                .Where(c => c.Connector.WireTransformer != null)
                .Select(c => (c.Connector, Distance: Vector3Int.Distance(position, c.Position)))
                .Where(c => IsGeometricallyConnectable(c.Distance, ownParam.MaxWireLength, c.Connector))
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Connector != null && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole.Connector.BlockInstanceId, nearestPole.Connector, nearestPole.Distance));
                usedCount++;
            }

            // ②範囲内の未接続機械/発電機を残り本数まで収集する
            // Collect unconnected machines/generators within range up to remaining capacity
            results.AddRange(CollectPoleMachineTargets(ownParam, position, usedCount));

            return results;
        }

        // レール式延長で使う。起点との明示接続分を差し引いた残り本数で機械のみを収集する
        // Used by rail-style extend; collects machines only, given the capacity already spent on the explicit origin wire
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleMachineTargets(ElectricPoleBlockParam ownParam, Vector3Int position, int usedCount)
        {
            var results = new List<(BlockInstanceId, IElectricWireConnector, float)>();

            // 範囲内のワイヤー0本の機械/発電機を近い順に残り本数まで
            // Unconnected machines/generators within machine range, nearest first, up to remaining capacity
            var machineCandidates = CollectConnectors(ElectricConnectionRangeService.EnumerateMachineRange(position, ownParam))
                .Where(c => (c.Connector.WireConsumer != null || c.Connector.WireGenerator != null) && c.Connector.WireConnections.Count == 0)
                .Select(c => (c.Connector, Distance: Vector3Int.Distance(position, c.Position)))
                .Where(c => IsGeometricallyConnectable(c.Distance, ownParam.MaxWireLength, c.Connector))
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive());

            foreach (var candidate in machineCandidates)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((candidate.Connector.BlockInstanceId, candidate.Connector, candidate.Distance));
                usedCount++;
            }

            return results;
        }

        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectMachineTargets(BlockMasterElement blockMaster, Vector3Int position, BlockDirection direction, float ownMaxWireLength)
        {
            var datastore = ServerContext.WorldBlockDatastore;
            var maxRange = ServerContext.GetService<MaxElectricPoleMachineConnectionRange>();
            var machinePositionInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);

            // 接続可能な最寄り電柱1本を対象にする
            // Only the nearest connectable pole whose machine range covers this position
            var nearestPole = ElectricConnectionRangeService
                .EnumerateCandidatePolePositions(machinePositionInfo, maxRange.GetHorizontal(), maxRange.GetHeight())
                .SelectMany(polePos => ResolvePoleAt(polePos))
                .Where(c => ElectricConnectionRangeService.IsWithinMachineRange(machinePositionInfo, datastore.GetBlockPosition(c.Connector.BlockInstanceId), c.PoleParam))
                .Select(c => (c.Connector, Distance: Vector3Int.Distance(position, datastore.GetBlockPosition(c.Connector.BlockInstanceId))))
                .Where(c => IsGeometricallyConnectable(c.Distance, ownMaxWireLength, c.Connector))
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Connector == null) return new List<(BlockInstanceId, IElectricWireConnector, float)>();

            return new List<(BlockInstanceId, IElectricWireConnector, float)> { (nearestPole.Connector.BlockInstanceId, nearestPole.Connector, nearestPole.Distance) };

            #region Internal

            IEnumerable<(IElectricWireConnector Connector, ElectricPoleBlockParam PoleParam)> ResolvePoleAt(Vector3Int polePos)
            {
                if (!datastore.TryGetBlock<IElectricWireConnector>(polePos, out var connector) || connector.WireTransformer == null)
                    yield break;

                var poleParam = datastore.GetBlock(connector.BlockInstanceId).BlockMasterElement.BlockParam as ElectricPoleBlockParam;
                if (poleParam == null) yield break;

                yield return (connector, poleParam);
            }

            #endregion
        }

        // 距離・両端の最大接続距離・接続数上限のみを純粋判定に委ねる。所持アイテムはダミー値で無視する
        // Delegate distance/max-length/capacity checks to the pure evaluator; item availability is probed away
        private static bool IsGeometricallyConnectable(float distance, float selfMaxWireLength, IElectricWireConnector target)
        {
            var probe = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, selfMaxWireLength, target.MaxWireLength,
                false, target.IsWireConnectionFull,
                ItemMaster.EmptyItemId, System.Array.Empty<IItemStack>(), ItemMaster.EmptyItemId);

            return probe.FailureReason != ElectricWirePlacementEvaluator.TooFarError
                   && probe.FailureReason != ElectricWirePlacementEvaluator.AlreadyConnectedError
                   && probe.FailureReason != ElectricWirePlacementEvaluator.ConnectionLimitError;
        }

        // 範囲内のグリッド座標を走査し、重複を除いたワイヤー端点を収集する
        // Scan grid positions in range and collect deduplicated wire endpoints
        private static List<(Vector3Int Position, IElectricWireConnector Connector)> CollectConnectors(IEnumerable<Vector3Int> range)
        {
            var datastore = ServerContext.WorldBlockDatastore;
            var found = new Dictionary<BlockInstanceId, IElectricWireConnector>();
            foreach (var pos in range)
            {
                if (!datastore.TryGetBlock<IElectricWireConnector>(pos, out var connector)) continue;
                found.TryAdd(connector.BlockInstanceId, connector);
            }

            return found.Values.Select(c => (datastore.GetBlockPosition(c.BlockInstanceId), c)).ToList();
        }
    }
}
