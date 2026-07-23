using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 自動接続候補を受信済みクライアント状態のみで収集する。サーバーのワールド状態には触れない
    /// Collects auto-connect candidates using only received client state; never touches server world state
    /// 収集ルールはサーバーのElectricWireAutoConnectTargetCollectorと同一（相互範囲判定＋最寄り電柱1本＋未接続機械）
    /// The rules mirror the server's ElectricWireAutoConnectTargetCollector (mutual range, nearest pole, unconnected machines)
    /// </summary>
    public static class ClientElectricWireAutoConnectCollector
    {
        public static List<(Vector3Int TargetPos, float Distance)> Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // 自身の接続容量が0なら探索するまでもなく対象なし
            // No point searching when this block has zero connection capacity
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out var ownCapacity, out var ownProfile, out var ownIsPole) || ownCapacity <= 0)
                return new List<(Vector3Int, float)>();

            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);

            return ownIsPole
                ? CollectPoleTargets((ElectricPoleBlockParam)blockMaster.BlockParam, ownInfo, ownProfile, blockDataStore)
                : CollectMachineTargets(ownInfo, ownProfile, blockDataStore);
        }

        // 電柱設置: 最寄り電柱1本＋相互範囲内の未接続機械を残り本数まで
        // Pole placement: nearest pole plus unconnected machines mutually in range up to remaining capacity
        private static List<(Vector3Int, float)> CollectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, BlockGameObjectDataStore dataStore)
        {
            var results = new List<(Vector3Int, float)>();
            var usedCount = 0;

            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, true, dataStore)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Block != null && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole.Block.BlockPosInfo.OriginalPos, nearestPole.Distance));
                usedCount++;
            }

            var machineCandidates = EnumerateConnectableCandidates(ownInfo, ownProfile, true, dataStore)
                .Where(c => !c.IsPole && GetPartnerCount(c.Block) == 0)
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive());

            foreach (var candidate in machineCandidates)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((candidate.Block.BlockPosInfo.OriginalPos, candidate.Distance));
                usedCount++;
            }

            return results;
        }

        // 機械設置: 相互範囲内の最寄り電柱1本のみ
        // Machine placement: only the nearest pole mutually in range
        private static List<(Vector3Int, float)> CollectMachineTargets(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, BlockGameObjectDataStore dataStore)
        {
            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, false, dataStore)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Block == null) return new List<(Vector3Int, float)>();

            return new List<(Vector3Int, float)> { (nearestPole.Block.BlockPosInfo.OriginalPos, nearestPole.Distance) };
        }

        // 受信済み全ブロックから、相互範囲内で接続上限未満のワイヤー端点を距離付きで列挙する
        // Enumerate wire endpoints mutually in range and below capacity from all received blocks, with distances
        private static IEnumerable<(BlockGameObject Block, bool IsPole, float Distance)> EnumerateConnectableCandidates(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, bool ownIsPole, BlockGameObjectDataStore dataStore)
        {
            foreach (var block in dataStore.BlockGameObjectByInstanceIdDictionary.Values)
            {
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(block.BlockMasterElement.BlockParam, out var capacity, out var targetProfile, out var targetIsPole)) continue;
                if (capacity <= GetPartnerCount(block)) continue;
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, block.BlockPosInfo, targetProfile, targetIsPole)) continue;

                yield return (block, targetIsPole, Vector3Int.Distance(ownInfo.OriginalPos, block.BlockPosInfo.OriginalPos));
            }
        }

        // 受信済みワイヤー状態から接続本数を得る
        // Read the connection count from received wire state
        private static int GetPartnerCount(BlockGameObject block)
        {
            return block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) ? processor.CurrentPartnerIds.Count : 0;
        }
    }
}
