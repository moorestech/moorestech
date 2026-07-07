using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 自動接続候補を受信済みクライアント状態のみで収集する。サーバーのワールド状態には触れない
    /// Collects auto-connect candidates using only received client state; never touches server world state
    /// 収集ルールはサーバーのElectricWireAutoConnectTargetCollectorと同一（最寄り電柱1本＋未接続機械）
    /// The selection rules mirror the server's ElectricWireAutoConnectTargetCollector (nearest pole + unconnected machines)
    /// 注意: クライアント辞書は原点座標登録のみのため、複数セルブロックの範囲判定は原点基準の近似となる
    /// Note: the client dictionary registers origin cells only, so range checks for multi-cell blocks approximate by origin
    /// </summary>
    public static class ClientElectricWireAutoConnectCollector
    {
        // 電柱の最大機械接続範囲。マスタから一度だけ算出する
        // Max pole-to-machine connection range, computed once from master data
        private static MaxElectricPoleMachineConnectionRange _maxRange;

        public static List<(Vector3Int TargetPos, float Distance)> Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // 自身の接続容量が0なら探索するまでもなく対象なし
            // No point searching when this block has zero connection capacity
            if (!ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, out var ownCapacity, out var ownMaxLength) || ownCapacity <= 0)
                return new List<(Vector3Int, float)>();

            return blockMaster.BlockParam is ElectricPoleBlockParam poleParam
                ? CollectPoleTargets(poleParam, position, ownMaxLength, blockDataStore)
                : CollectMachineTargets(blockMaster, position, direction, ownMaxLength, blockDataStore);
        }

        // 電柱設置: 最寄り電柱1本＋範囲内の未接続機械を残り本数まで
        // Pole placement: nearest pole plus unconnected machines up to remaining capacity
        private static List<(Vector3Int, float)> CollectPoleTargets(ElectricPoleBlockParam ownParam, Vector3Int position, float ownMaxLength, BlockGameObjectDataStore dataStore)
        {
            var results = new List<(Vector3Int, float)>();
            var usedCount = 0;

            var nearestPole = CollectWireBlocks(ElectricConnectionRangeService.EnumeratePoleRange(position, ownParam), dataStore)
                .Where(b => b.BlockMasterElement.BlockParam is ElectricPoleBlockParam)
                .Select(b => (Block: b, Distance: Vector3Int.Distance(position, b.BlockPosInfo.OriginalPos)))
                .Where(c => IsGeometricallyConnectable(c.Distance, ownMaxLength, c.Block))
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Block != null && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole.Block.BlockPosInfo.OriginalPos, nearestPole.Distance));
                usedCount++;
            }

            var machineCandidates = CollectWireBlocks(ElectricConnectionRangeService.EnumerateMachineRange(position, ownParam), dataStore)
                .Where(b => b.BlockMasterElement.BlockParam is not ElectricPoleBlockParam && GetPartnerCount(b) == 0)
                .Select(b => (Block: b, Distance: Vector3Int.Distance(position, b.BlockPosInfo.OriginalPos)))
                .Where(c => IsGeometricallyConnectable(c.Distance, ownMaxLength, c.Block))
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive());

            foreach (var candidate in machineCandidates)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((candidate.Block.BlockPosInfo.OriginalPos, candidate.Distance));
                usedCount++;
            }

            return results;
        }

        // 機械設置: 機械範囲でこの位置を覆う最寄り電柱1本のみ
        // Machine placement: only the nearest pole whose machine range covers this position
        private static List<(Vector3Int, float)> CollectMachineTargets(BlockMasterElement master, Vector3Int position, BlockDirection direction, float ownMaxLength, BlockGameObjectDataStore dataStore)
        {
            _maxRange ??= new MaxElectricPoleMachineConnectionRange();
            var machineInfo = new BlockPositionInfo(position, direction, master.BlockSize);

            var nearestPole = ElectricConnectionRangeService
                .EnumerateCandidatePolePositions(machineInfo, _maxRange.GetHorizontal(), _maxRange.GetHeight())
                .SelectMany(polePos => ResolvePoleAt(polePos, dataStore))
                .Where(c => ElectricConnectionRangeService.IsWithinMachineRange(machineInfo, c.Block.BlockPosInfo.OriginalPos, c.PoleParam))
                .Select(c => (c.Block, Distance: Vector3Int.Distance(position, c.Block.BlockPosInfo.OriginalPos)))
                .Where(c => IsGeometricallyConnectable(c.Distance, ownMaxLength, c.Block))
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Block == null) return new List<(Vector3Int, float)>();

            return new List<(Vector3Int, float)> { (nearestPole.Block.BlockPosInfo.OriginalPos, nearestPole.Distance) };
        }

        private static IEnumerable<(BlockGameObject Block, ElectricPoleBlockParam PoleParam)> ResolvePoleAt(Vector3Int polePos, BlockGameObjectDataStore dataStore)
        {
            if (!dataStore.TryGetBlockGameObject(polePos, out var block)) yield break;
            if (block.BlockMasterElement.BlockParam is not ElectricPoleBlockParam poleParam) yield break;

            yield return (block, poleParam);
        }

        // 距離・両端の最大接続距離・接続数上限のみで判定する（サーバーと同じ幾何判定）
        // Judge by distance, both max wire lengths and capacity only (same geometric rule as the server)
        private static bool IsGeometricallyConnectable(float distance, float ownMaxLength, BlockGameObject target)
        {
            if (!ElectricWireBlockParamResolver.TryGetWireParam(target.BlockMasterElement.BlockParam, out var targetCapacity, out var targetMaxLength)) return false;
            if (Mathf.Min(ownMaxLength, targetMaxLength) < distance) return false;

            return GetPartnerCount(target) < targetCapacity;
        }

        // 受信済みワイヤー状態から接続本数を得る
        // Read the connection count from received wire state
        private static int GetPartnerCount(BlockGameObject block)
        {
            return block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) ? processor.CurrentPartnerIds.Count : 0;
        }

        // 範囲内の原点座標を走査し、重複を除いたワイヤー端点ブロックを収集する
        // Scan origin positions in range and collect deduplicated wire endpoint blocks
        private static IEnumerable<BlockGameObject> CollectWireBlocks(IEnumerable<Vector3Int> range, BlockGameObjectDataStore dataStore)
        {
            var found = new Dictionary<BlockInstanceId, BlockGameObject>();
            foreach (var pos in range)
            {
                if (!dataStore.TryGetBlockGameObject(pos, out var block)) continue;
                if (!ElectricWireBlockParamResolver.TryGetWireParam(block.BlockMasterElement.BlockParam, out _, out _)) continue;
                found.TryAdd(block.BlockInstanceId, block);
            }

            return found.Values;
        }
    }
}
