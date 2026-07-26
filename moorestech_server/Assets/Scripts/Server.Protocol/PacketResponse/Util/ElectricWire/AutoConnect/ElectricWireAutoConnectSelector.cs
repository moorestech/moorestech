using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 選定アルゴリズム本体。サーバー/クライアント共用純粋ロジック
    /// 選定ルール: 最寄り電柱1本→未接続機械を残容量まで。順序は距離昇順→InstanceId昇順
    /// candidatesには自ブロック自身を含めてはならない（含めると距離0で必ず最優先に選ばれる）
    /// Auto-connect selection algorithm; pure logic shared by server and client
    /// Rule: nearest pole first, then unconnected machines up to remaining capacity, ordered by distance then id
    /// candidates must never include the caller's own block (it would win priority at distance 0)
    /// </summary>
    public static class ElectricWireAutoConnectSelector
    {
        // 設置時の選定入口。電柱か機械かの分岐はここ1箇所に閉じる
        // The placement entry point; the pole-vs-machine branch lives only here
        public static List<(BlockInstanceId TargetId, float Distance)> SelectPlacementTargets(IBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            return ownParam is ElectricPoleBlockParam poleParam
                ? SelectPoleTargets(poleParam, ownInfo, candidates)
                : SelectMachineTargets(ownParam, ownInfo, candidates);
        }

        // 電柱設置: 最寄り電柱1本＋未接続機械を残容量まで
        // Pole placement: nearest pole plus unconnected machines up to remaining capacity
        private static List<(BlockInstanceId TargetId, float Distance)> SelectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            var results = new List<(BlockInstanceId, float)>();
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);
            var usedCount = 0;

            // 相互範囲内で接続可能な最寄り電柱1本
            // The single nearest mutually-in-range connectable pole
            var nearestPole = EnumerateConnectable(ownInfo, ownProfile, true, candidates)
                .Where(c => c.IsPole)
                .Take(1).ToList();

            if (nearestPole.Count == 1 && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole[0].InstanceId, nearestPole[0].Distance));
                usedCount++;
            }

            results.AddRange(SelectPoleMachineTargetsCore(ownParam, ownInfo, usedCount, candidates, ownProfile));
            return results;
        }

        // レール延長でも使用。残容量で機械のみ収集
        // Also used by rail extend; collects machines within remaining capacity
        public static List<(BlockInstanceId TargetId, float Distance)> SelectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            return SelectPoleMachineTargetsCore(ownParam, ownInfo, usedCount, candidates, ConnectionRangeProfile.CreatePole(ownParam));
        }

        // 機械設置: 相互範囲内の最寄り電柱1本のみ
        // Machine placement: only the nearest mutually-in-range pole
        private static List<(BlockInstanceId TargetId, float Distance)> SelectMachineTargets(IBlockParam ownParam, BlockPositionInfo ownInfo, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            // 自分は設置直後で接続数0固定。電柱側の残容量判定と表現を揃える
            // Self is freshly placed with zero existing connections; mirrors the pole-side remaining-capacity check
            var ownUsedCount = 0;
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(ownParam, out var ownCapacity, out var ownProfile, out var ownIsPole) || ownCapacity <= ownUsedCount)
                return new List<(BlockInstanceId, float)>();

            return EnumerateConnectable(ownInfo, ownProfile, ownIsPole, candidates)
                .Where(c => c.IsPole)
                .Take(1)
                .Select(c => (c.InstanceId, c.Distance))
                .ToList();
        }

        // 残容量で、既存プロファイルを使い回し機械のみ収集
        // Collects machines within remaining capacity, reusing the built profile
        private static List<(BlockInstanceId, float)> SelectPoleMachineTargetsCore(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount, IReadOnlyList<ElectricWireConnectCandidate> candidates, ConnectionRangeProfile ownProfile)
        {
            var results = new List<(BlockInstanceId, float)>();

            // 範囲内未接続機械を近い順に残容量まで
            // Unconnected machines in range, nearest first, up to capacity
            var machines = EnumerateConnectable(ownInfo, ownProfile, true, candidates)
                .Where(c => !c.IsPole && c.ConnectionCount == 0);

            foreach (var machine in machines)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((machine.InstanceId, machine.Distance));
                usedCount++;
            }

            return results;
        }

        // 候補列から、相互範囲内で容量未満のワイヤー端点を、距離→InstanceId順の全順序で列挙する
        // Enumerate endpoints mutually in range and below capacity, in full order by distance then id
        private static IReadOnlyList<(BlockInstanceId InstanceId, bool IsPole, int ConnectionCount, float Distance)> EnumerateConnectable(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, bool ownIsPole, IReadOnlyList<ElectricWireConnectCandidate> candidates)
        {
            var results = new List<(BlockInstanceId InstanceId, bool IsPole, int ConnectionCount, float Distance)>();

            foreach (var candidate in candidates)
            {
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(candidate.BlockParam, out var capacity, out var profile, out var isPole)) continue;
                if (capacity <= candidate.CurrentConnectionCount) continue;
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, candidate.PositionInfo, profile, isPole)) continue;

                // 距離は原点座標同士。順序付けとコスト計算にのみ使う
                // Distance between origin cells; used only for ordering and cost
                results.Add((candidate.InstanceId, isPole, candidate.CurrentConnectionCount, Vector3Int.Distance(ownInfo.OriginalPos, candidate.PositionInfo.OriginalPos)));
            }

            return results.OrderBy(c => c.Distance).ThenBy(c => c.InstanceId.AsPrimitive()).ToList();
        }
    }
}
