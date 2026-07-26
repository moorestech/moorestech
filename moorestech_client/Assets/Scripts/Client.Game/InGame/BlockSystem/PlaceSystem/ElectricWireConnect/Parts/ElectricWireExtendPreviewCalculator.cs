using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Item.Interface;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
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
        public static ElectricWirePlacementJudgement Evaluate(BlockGameObject source, BlockGameObject target, int sourceMaxConnectionCount, int targetMaxConnectionCount, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems)
        {
            // 範囲相互判定に失敗したらOutOfRangeで確定する
            // Fail fast with OutOfRange when the mutual range check does not pass
            if (!IsMutuallyInRange(source, target)) return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.OutOfRange);

            var alreadyConnected = IsAlreadyConnected(source, target);
            var anyConnectionFull = IsConnectionFull(source, sourceMaxConnectionCount) || IsConnectionFull(target, targetMaxConnectionCount);

            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, alreadyConnected, anyConnectionFull, connectToolGuid, inventoryItems, null);
        }

        /// <summary>
        /// 新設電柱への延長可否を評価する。新設側は未接続のため起点の状態のみ内部で判定する
        /// Evaluate extending to a newly placed pole; only the origin's state matters since the new pole has no connections
        /// </summary>
        public static ElectricWirePlacementJudgement EvaluateNewPole(BlockGameObject source, int sourceMaxConnectionCount, ElectricPoleBlockParam poleParam, BlockPositionInfo poleGhostInfo, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems)
        {
            // 起点と新設電柱ゴーストの範囲相互判定を行う
            // Mutual range check between the origin and the new pole ghost
            if (!TryResolveWireParam(source, out _, out var sourceProfile, out var sourceIsPole))
                return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.InvalidTarget);
            if (!ElectricConnectionRangeService.IsMutuallyConnectable(source.BlockPosInfo, sourceProfile, sourceIsPole, poleGhostInfo, ConnectionRangeProfile.CreatePole(poleParam), true))
                return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.OutOfRange);

            var sourceFull = IsConnectionFull(source, sourceMaxConnectionCount);

            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, false, sourceFull, connectToolGuid, inventoryItems, null);
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
}
