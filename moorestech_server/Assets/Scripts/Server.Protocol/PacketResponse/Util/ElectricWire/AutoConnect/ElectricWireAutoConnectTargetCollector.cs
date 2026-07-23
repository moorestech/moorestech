using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 設置位置の周辺から自動接続対象の候補を収集する。電柱設置と機械設置で選定ルールが異なる
    /// Collects auto-connect target candidates around a placement position; rules differ for pole vs machine
    /// 判定はワールド全コネクタ列挙＋範囲ボックス相互判定。距離は最寄り順序付けとコスト計算にのみ使う
    /// Judged by enumerating all world connectors with mutual range boxes; distance is used only for ordering and cost
    /// </summary>
    public static class ElectricWireAutoConnectTargetCollector
    {
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo)
        {
            var results = new List<(BlockInstanceId, IElectricWireConnector, float)>();
            var usedCount = 0;
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);

            // ①相互範囲内で接続可能な最寄り電柱1本
            // Nearest mutually-in-range connectable pole
            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, true)
                .Where(c => c.Connector.EnergyRole is IElectricTransformer)
                .Where(c => !c.Connector.IsWireConnectionFull)
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Connector != null && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole.Connector.BlockInstanceId, nearestPole.Connector, nearestPole.Distance));
                usedCount++;
            }

            // ②相互範囲内の未接続機械/発電機を残数まで収集
            // Collect unconnected machines/generators mutually in range up to remaining capacity
            results.AddRange(CollectPoleMachineTargets(ownParam, ownInfo, usedCount));

            return results;
        }

        // レール式延長で使う。起点との明示接続分を差し引いた残り本数で機械のみを収集する
        // Used by rail-style extend; collects machines only, given the capacity already spent on the explicit origin wire
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount)
        {
            var results = new List<(BlockInstanceId, IElectricWireConnector, float)>();
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);

            // 相互範囲内の未接続機械/発電機を近い順に残数まで
            // Unconnected machines/generators mutually in range, nearest first, up to remaining capacity
            var machineCandidates = EnumerateConnectableCandidates(ownInfo, ownProfile, true)
                .Where(c => c.Connector.EnergyRole is IElectricConsumer or IElectricGenerator && c.Connector.WireConnections.Count == 0)
                .Where(c => !c.Connector.IsWireConnectionFull)
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive());

            foreach (var candidate in machineCandidates)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((candidate.Connector.BlockInstanceId, candidate.Connector, candidate.Distance));
                usedCount++;
            }

            return results;
        }

        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectMachineTargets(BlockMasterElement blockMaster, BlockPositionInfo ownInfo)
        {
            // 自身の範囲プロファイルを解決する（非電気系は対象なし）
            // Resolve own range profile (non-electric yields no targets)
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out var ownProfile, out var ownIsPole))
                return new List<(BlockInstanceId, IElectricWireConnector, float)>();

            // 相互範囲内で接続可能な最寄り電柱1本のみ
            // Only the nearest mutually-in-range connectable pole
            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, ownIsPole)
                .Where(c => c.Connector.EnergyRole is IElectricTransformer)
                .Where(c => !c.Connector.IsWireConnectionFull)
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Connector == null) return new List<(BlockInstanceId, IElectricWireConnector, float)>();

            return new List<(BlockInstanceId, IElectricWireConnector, float)> { (nearestPole.Connector.BlockInstanceId, nearestPole.Connector, nearestPole.Distance) };
        }

        // ワールド全ブロックから、自分と相互範囲内にあるワイヤー端点を距離付きで列挙する
        // Enumerate wire endpoints mutually in range with self from all world blocks, with distances
        private static IEnumerable<(IElectricWireConnector Connector, float Distance)> EnumerateConnectableCandidates(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, bool ownIsPole)
        {
            var datastore = ServerContext.WorldBlockDatastore;
            foreach (var worldBlock in datastore.BlockMasterDictionary.Values)
            {
                if (!worldBlock.Block.TryGetComponent<IElectricWireConnector>(out var connector)) continue;
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(worldBlock.Block.BlockMasterElement.BlockParam, out _, out var targetProfile, out var targetIsPole)) continue;
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, worldBlock.BlockPositionInfo, targetProfile, targetIsPole)) continue;

                // 距離は原点座標同士。順序付けとコスト計算にのみ使う
                // Distance between origin cells; used only for ordering and cost
                yield return (connector, Vector3Int.Distance(ownInfo.OriginalPos, worldBlock.BlockPositionInfo.OriginalPos));
            }
        }
    }
}
