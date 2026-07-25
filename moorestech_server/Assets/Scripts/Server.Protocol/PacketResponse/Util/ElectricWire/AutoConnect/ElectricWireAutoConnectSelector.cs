using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 自動接続の候補選定アルゴリズム本体。サーバー/クライアント双方から使う純粋ロジック
    /// The auto-connect selection algorithm itself; pure logic shared by server and client
    /// 選定ルール: 最寄り電柱1本→未接続機械を残容量まで。順序は距離昇順→InstanceId昇順
    /// Rule: nearest pole first, then unconnected machines up to remaining capacity, ordered by distance then id
    /// </summary>
    public static class ElectricWireAutoConnectSelector
    {
        // 電柱設置: 最寄り電柱1本＋未接続機械を残容量まで
        // Pole placement: nearest pole plus unconnected machines up to remaining capacity
        public static List<(BlockInstanceId TargetId, float Distance)> SelectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            var results = new List<(BlockInstanceId, float)>();
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);
            var usedCount = 0;

            // 相互範囲内で接続可能な最寄り電柱1本
            // The single nearest mutually-in-range connectable pole
            var nearestPole = EnumerateConnectable(ownInfo, ownProfile, true, candidates)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.InstanceId.AsPrimitive())
                .Take(1).ToList();

            if (nearestPole.Count == 1 && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole[0].InstanceId, nearestPole[0].Distance));
                usedCount++;
            }

            results.AddRange(SelectPoleMachineTargets(ownParam, ownInfo, usedCount, candidates));
            return results;
        }

        // レール式延長でも使う。使用済み本数を差し引いた残容量で機械のみを収集する
        // Also used by rail-style extend; collects machines only, within the capacity left after usedCount
        public static List<(BlockInstanceId TargetId, float Distance)> SelectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            var results = new List<(BlockInstanceId, float)>();
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);

            // 相互範囲内の未接続機械を近い順に残容量まで
            // Unconnected machines mutually in range, nearest first, up to remaining capacity
            var machines = EnumerateConnectable(ownInfo, ownProfile, true, candidates)
                .Where(c => !c.IsPole && c.ConnectionCount == 0)
                .OrderBy(c => c.Distance).ThenBy(c => c.InstanceId.AsPrimitive());

            foreach (var machine in machines)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((machine.InstanceId, machine.Distance));
                usedCount++;
            }

            return results;
        }

        // 機械設置: 相互範囲内の最寄り電柱1本のみ
        // Machine placement: only the nearest mutually-in-range pole
        public static List<(BlockInstanceId TargetId, float Distance)> SelectMachineTargets(IBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            // 自分が電気系でない・容量0なら対象なし
            // Non-electric or zero-capacity self yields no targets
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(ownParam, out var ownCapacity, out var ownProfile, out var ownIsPole) || ownCapacity <= 0)
                return new List<(BlockInstanceId, float)>();

            return EnumerateConnectable(ownInfo, ownProfile, ownIsPole, candidates)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.InstanceId.AsPrimitive())
                .Take(1)
                .Select(c => (c.InstanceId, c.Distance))
                .ToList();
        }

        // 候補列から、相互範囲内で容量未満のワイヤー端点を距離付きで列挙する
        // Enumerate endpoints mutually in range and below capacity, with distances
        private static IEnumerable<(BlockInstanceId InstanceId, bool IsPole, int ConnectionCount, float Distance)> EnumerateConnectable(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, bool ownIsPole, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(candidate.BlockParam, out var capacity, out var profile, out var isPole)) continue;
                if (capacity <= candidate.CurrentConnectionCount) continue;
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, candidate.PositionInfo, profile, isPole)) continue;

                // 距離は原点座標同士。順序付けとコスト計算にのみ使う
                // Distance between origin cells; used only for ordering and cost
                yield return (candidate.InstanceId, isPole, candidate.CurrentConnectionCount, Vector3Int.Distance(ownInfo.OriginalPos, candidate.PositionInfo.OriginalPos));
            }
        }
    }
}
