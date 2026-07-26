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
    /// 受信済み状態から候補組立、選定はSelectorへ委譲
    /// 選定ルールはサーバーと同一ソースを共有するため、プレビューと実接続の判定は構造的に一致する
    /// Builds candidates from client state; delegates selection to Selector
    /// Selection shares the server's source, so preview and actual connection judgements match structurally
    /// </summary>
    public static class ClientElectricWireAutoConnectCollector
    {
        public static List<(Vector3Int TargetPos, float Distance)> Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);
            var (candidates, positions) = BuildReceivedCandidates();

            var selected = ElectricWireAutoConnectSelector.SelectPlacementTargets(blockMaster.BlockParam, ownInfo, candidates);

            return selected.Select(s => (positions[s.TargetId], s.Distance)).ToList();

            #region Internal

            // ワイヤー状態を持つ受信ブロックのみ候補化
            // Only received blocks carrying the wire state processor become candidates
            (List<ElectricWireConnectCandidate> Candidates, Dictionary<BlockInstanceId, Vector3Int> Positions) BuildReceivedCandidates()
            {
                var built = new List<ElectricWireConnectCandidate>();
                var builtPositions = new Dictionary<BlockInstanceId, Vector3Int>();

                foreach (var block in blockDataStore.BlockGameObjectByInstanceIdDictionary.Values)
                {
                    if (!block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor)) continue;

                    built.Add(new ElectricWireConnectCandidate(block.BlockInstanceId, block.BlockMasterElement.BlockParam, block.BlockPosInfo, processor.CurrentPartnerIds.Count));
                    builtPositions[block.BlockInstanceId] = block.BlockPosInfo.OriginalPos;
                }

                return (built, builtPositions);
            }

            #endregion
        }
    }
}
