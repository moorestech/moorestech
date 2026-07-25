using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 受信済みクライアント状態から候補を組み立て、選定はElectricWireAutoConnectSelectorに委譲する
    /// Builds candidates from received client state and delegates selection to ElectricWireAutoConnectSelector
    /// 選定ルールはサーバーと同一ソースを共有するため、プレビューと実接続の判定は構造的に一致する
    /// Selection shares the server's source, so preview and actual connection judgements match structurally
    /// </summary>
    public static class ClientElectricWireAutoConnectCollector
    {
        public static List<(Vector3Int TargetPos, float Distance)> Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);
            var (candidates, positions) = BuildReceivedCandidates(blockDataStore);

            // 電柱設置と機械設置で選定ルールを切り替える
            // Switch selection rules between pole placement and machine placement
            var selected = blockMaster.BlockParam is ElectricPoleBlockParam poleParam
                ? ElectricWireAutoConnectSelector.SelectPoleTargets(poleParam, ownInfo, candidates)
                : ElectricWireAutoConnectSelector.SelectMachineTargets(blockMaster.BlockParam, ownInfo, candidates);

            return selected.Select(s => (positions[s.TargetId], s.Distance)).ToList();
        }

        // 受信済み全ブロックからワイヤー端点候補と座標逆引き表を組み立てる
        // Build endpoint candidates and a position lookup from all received blocks
        private static (List<ElectricWireConnectCandidate> Candidates, Dictionary<BlockInstanceId, Vector3Int> Positions) BuildReceivedCandidates(BlockGameObjectDataStore blockDataStore)
        {
            var candidates = new List<ElectricWireConnectCandidate>();
            var positions = new Dictionary<BlockInstanceId, Vector3Int>();

            foreach (var block in blockDataStore.BlockGameObjectByInstanceIdDictionary.Values)
            {
                var connectionCount = block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) ? processor.CurrentPartnerIds.Count : 0;

                candidates.Add(new ElectricWireConnectCandidate(block.BlockInstanceId, block.BlockMasterElement.BlockParam, block.BlockPosInfo, connectionCount));
                positions[block.BlockInstanceId] = block.BlockPosInfo.OriginalPos;
            }

            return (candidates, positions);
        }
    }
}
