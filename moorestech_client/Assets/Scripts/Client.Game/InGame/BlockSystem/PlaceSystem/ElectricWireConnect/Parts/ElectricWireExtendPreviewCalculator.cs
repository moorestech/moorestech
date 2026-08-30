using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.ElectricWire;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// クライアント側でワイヤー接続可否を評価する。範囲相互判定と評価器はサーバーとソース共有
    /// Evaluate wire connections on the client, sharing the mutual range check and evaluator with the server
    /// </summary>
    public static class ElectricWireExtendPreviewCalculator
    {
        /// <summary>
        /// ブロックが電気系（ワイヤー端点）かを判定し、接続数上限と範囲プロファイルを返す
        /// Judge whether a block is electric and return its connection limit and range profile
        /// </summary>
        public static bool TryResolveWireParam(BlockGameObject block, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            return TryResolveWireParam(block.BlockMasterElement, out maxWireConnectionCount, out rangeProfile, out isPole);
        }

        public static bool TryResolveWireParam(BlockMasterElement master, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            // 9種の電気系BlockParamから上限と範囲を取り出す（非電気系はfalse）
            // Extract limits and ranges from the 9 electric block params (non-electric returns false)
            return ElectricWireBlockParamResolver.TryGetWireRangeParam(master.BlockParam, out maxWireConnectionCount, out rangeProfile, out isPole);
        }

        /// <summary>
        /// 既存ブロック同士の接続可否を評価する。範囲相互判定→評価器の順で判定する
        /// Evaluate connecting two existing blocks: mutual range check first, then the evaluator
        /// </summary>
        public static ElectricWireExtendPreviewData Evaluate(BlockGameObject source, BlockGameObject target, int sourceMaxConnectionCount, int targetMaxConnectionCount, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems)
        {
            // 既設ブロック同士の接続はブロックを設置しないため予約は無い
            // Connecting two existing blocks places no block, so there is nothing to reserve
            // 範囲相互判定に失敗したらOutOfRangeで確定する
            // Fail fast with OutOfRange when the mutual range check does not pass
            if (!IsMutuallyInRange(source, target)) return BuildPreview(ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.OutOfRange), connectToolGuid, distance, inventoryItems, null);

            var alreadyConnected = IsAlreadyConnected(source, target);
            var anyConnectionFull = IsConnectionFull(source, sourceMaxConnectionCount) || IsConnectionFull(target, targetMaxConnectionCount);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, alreadyConnected, anyConnectionFull, connectToolGuid, inventoryItems, null);
            return BuildPreview(judgement, connectToolGuid, distance, inventoryItems, null);
        }

        /// <summary>
        /// 新設電柱への延長可否を評価する。新設側は未接続のため起点の状態のみ内部で判定する
        /// Evaluate extending to a newly placed pole; only the origin's state matters since the new pole has no connections
        /// 電柱の建設コストは同一フレームで先に押さえられるため、予約として電線判定と不足算出の双方へ載せる
        /// The new pole's construction cost is claimed first in the same frame, so it is reserved for both the wire judgement and the shortage calculation
        /// </summary>
        public static ElectricWireExtendPreviewData EvaluateNewPole(BlockGameObject source, int sourceMaxConnectionCount, ElectricPoleBlockParam poleParam, BlockPositionInfo poleGhostInfo, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<(ItemId itemId, int count)> poleConstructionItemCounts)
        {
            var reservedMaterials = ConnectToolMaterialConsumer.ToMaterials(poleConstructionItemCounts);

            // 起点と新設電柱ゴーストの範囲相互判定を行う
            // Mutual range check between the origin and the new pole ghost
            if (!TryResolveWireParam(source, out _, out var sourceProfile, out var sourceIsPole))
                return BuildPreview(ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.InvalidTarget), connectToolGuid, distance, inventoryItems, reservedMaterials);
            if (!ElectricConnectionRangeService.IsMutuallyConnectable(source.BlockPosInfo, sourceProfile, sourceIsPole, poleGhostInfo, ConnectionRangeProfile.CreatePole(poleParam), true))
                return BuildPreview(ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.OutOfRange), connectToolGuid, distance, inventoryItems, reservedMaterials);

            var sourceFull = IsConnectionFull(source, sourceMaxConnectionCount);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, false, sourceFull, connectToolGuid, inventoryItems, reservedMaterials);
            return BuildPreview(judgement, connectToolGuid, distance, inventoryItems, reservedMaterials);
        }

        // 判定と、その判定が使ったのと同じ入力から導いた不足素材・電線消費数を1つの表示データにまとめる
        // Bundles the judgement with the shortages and wire cost derived from the very inputs that judgement used
        private static ElectricWireExtendPreviewData BuildPreview(ElectricWirePlacementJudgement judgement, Guid connectToolGuid, float distance, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            return new ElectricWireExtendPreviewData(judgement, ResolveMaterialShortages(), ResolveCostCount());

            #region Internal

            // 素材不足で落ちたときだけ不足素材を算出する（他の理由では行が不要）
            // Derive the short materials only on a material-shortage failure (other reasons need no line)
            IReadOnlyList<ConstructionMaterialShortage> ResolveMaterialShortages()
            {
                if (judgement.FailureReason != ElectricWirePlacementFailureReason.NoWireItem) return Array.Empty<ConstructionMaterialShortage>();
                return ConnectToolMaterialShortageCalculator.Calculate(connectToolGuid, distance, inventoryItems, reservedMaterials);
            }

            // 成功/失敗どちらもコストを返す(失敗時は距離算出)
            // Returns a cost on success or failure (failure derives it from distance)
            int ResolveCostCount()
            {
                if (judgement.IsPlaceable) return judgement.WireCost.TotalCount;
                return ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, distance, out var cost) ? cost.TotalCount : 0;
            }

            #endregion
        }

        // 双方のプロファイルを解決して相互範囲判定にかける
        // Resolve both profiles and run the mutual range check
        private static bool IsMutuallyInRange(BlockGameObject blockA, BlockGameObject blockB)
        {
            if (!TryResolveWireParam(blockA, out _, out var profileA, out var isPoleA)) return false;
            if (!TryResolveWireParam(blockB, out _, out var profileB, out var isPoleB)) return false;

            return ElectricConnectionRangeService.IsMutuallyConnectable(blockA.BlockPosInfo, profileA, isPoleA, blockB.BlockPosInfo, profileB, isPoleB);
        }

        // どちらか一方の接続先集合に相手が含まれていれば接続済み
        // Connected when either side's partner set contains the other
        private static bool IsAlreadyConnected(BlockGameObject blockA, BlockGameObject blockB)
        {
            if (blockA.TryGetComponent<ElectricWireStateChangeProcessor>(out var processorA) &&
                processorA.CurrentPartnerIds.Contains(blockB.BlockInstanceId)) return true;

            return blockB.TryGetComponent<ElectricWireStateChangeProcessor>(out var processorB) &&
                   processorB.CurrentPartnerIds.Contains(blockA.BlockInstanceId);
        }

        // 受信済みワイヤー状態とマスタ上限から接続数が満杯かを判定する
        // Judge whether the connection count is full, using received wire state and the master limit
        private static bool IsConnectionFull(BlockGameObject block, int maxWireConnectionCount)
        {
            return block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) &&
                   maxWireConnectionCount <= processor.CurrentPartnerIds.Count;
        }
    }

    /// <summary>
    /// 電線接続・延長プレビューの表示データ。判定と表示値の対応付けを型で保証する（前例: GearChainPoleExtendPreviewData）
    /// Display data for the wire connect/extend preview; the type guarantees the judgement and its display values stay paired (precedent: GearChainPoleExtendPreviewData)
    /// </summary>
    public readonly struct ElectricWireExtendPreviewData
    {
        public readonly ElectricWirePlacementJudgement Judgement;

        // 素材不足時のみ非空、他は空
        // Non-empty only on a material shortage; empty otherwise
        public readonly IReadOnlyList<ConstructionMaterialShortage> MaterialShortages;

        // 表示する消費電線数。可否に関わらず接続距離から確定する
        // The wire count to display, resolved from the connection distance regardless of placeability
        public readonly int WireCostCount;

        public bool IsPlaceable => Judgement.IsPlaceable;

        public ElectricWireExtendPreviewData(ElectricWirePlacementJudgement judgement, IReadOnlyList<ConstructionMaterialShortage> materialShortages, int wireCostCount)
        {
            Judgement = judgement;
            MaterialShortages = materialShortages;
            WireCostCount = wireCostCount;
        }
    }
}
