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
    /// 受信済み状態から候補組立、選定はSelectorへ委譲
    /// 選定ルールはサーバーと同一ソースを共有するため、プレビューと実接続の判定は構造的に一致する
    /// Builds candidates from client state; delegates selection to Selector
    /// Selection shares the server's source, so preview and actual connection judgements match structurally
    /// </summary>
    public static class ClientElectricWireAutoConnectCollector
    {
        // 案内を出す近傍の広さ。自身の接続範囲の何倍までを「惜しくも届かなかった」とみなすか
        // How wide the advisory neighborhood is: how many times the block's own connection range still counts as "just missed"
        private const float InfoSearchRangeMultiplier = 2f;

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

        /// <summary>
        /// 設置セル近傍に、接続範囲外という理由で候補から落ちた電気ブロックがあるかを判定する
        /// Judge whether a nearby electric block was dropped from the candidates because it is out of connection range
        /// </summary>
        public static bool ExistsElectricNeighborOutOfConnectionRange(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out var ownProfile, out var ownIsPole)) return false;

            // 探索半径はマスタの接続範囲から導く。固定値だと高圧電柱のような長距離電柱で案内が一切出ない
            // Derive the search radius from the master's connection range; a fixed value silences the advice for long-range poles
            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);
            var searchRadius = MaxRange(ownProfile) * InfoSearchRangeMultiplier;

            foreach (var block in blockDataStore.BlockGameObjectByInstanceIdDictionary.Values)
            {
                if (!block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor)) continue;
                if (searchRadius < Vector3Int.Distance(block.BlockPosInfo.OriginalPos, position)) continue;
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(block.BlockMasterElement.BlockParam, out var capacity, out var profile, out var isPole)) continue;

                // 範囲以外の理由で落ちる相手を「範囲外」と案内しない。接続上限・既接続の機械・機械同士は選定規則が距離によらず除外する
                // Never report neighbors dropped for other reasons: capacity, already-wired machines and machine-to-machine are excluded by the selection rule regardless of distance
                var connectionCount = processor.CurrentPartnerIds.Count;
                if (capacity <= connectionCount) continue;
                if (!IsSelectableKind(isPole, connectionCount)) continue;

                // 残るのは範囲判定だけで落ちた近傍。サーバーと共有の相互判定を使う
                // What remains is dropped by the range check alone; use the server-shared mutual judgement
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, block.BlockPosInfo, profile, isPole)) return true;
            }
            return false;

            #region Internal

            // 選定規則（ElectricWireAutoConnectSelector）と同じ相手種別の制約。機械は電柱としか繋がらず、電柱は未接続の機械としか繋がらない
            // The same target-kind constraint as the selection rule: machines only pair with poles, and poles only take unwired machines
            bool IsSelectableKind(bool targetIsPole, int targetConnectionCount)
            {
                if (!ownIsPole) return targetIsPole;
                return targetIsPole || targetConnectionCount == 0;
            }

            // 3D距離と比較するため、相手種別ごとの各辺のうち最大を半径の近似に使う
            // Compare against 3D distance, so approximate the radius with the largest side across both target kinds
            float MaxRange(ConnectionRangeProfile profile)
            {
                return Mathf.Max(
                    Mathf.Max(profile.HorizontalAgainstPole, profile.HeightAgainstPole),
                    Mathf.Max(profile.HorizontalAgainstMachine, profile.HeightAgainstMachine));
            }

            #endregion
        }
    }
}
