using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// クライアント側でワイヤー接続可否を評価する。判定はサーバーと同じEvaluatorを共有する
    /// Evaluate wire connection eligibility on the client, sharing the same server-side Evaluator
    /// </summary>
    public static class ElectricWireExtendPreviewCalculator
    {
        /// <summary>
        /// ブロックが電気系（ワイヤー端点）かを判定し、接続数上限と最大ワイヤー長を返す
        /// Judge whether a block is electric (wire endpoint) and return its connection limit and max wire length
        /// </summary>
        public static bool TryResolveWireParam(BlockGameObject block, out int maxWireConnectionCount, out float maxWireLength)
        {
            return TryResolveWireParam(block.BlockMasterElement, out maxWireConnectionCount, out maxWireLength);
        }

        public static bool TryResolveWireParam(BlockMasterElement master, out int maxWireConnectionCount, out float maxWireLength)
        {
            // 7種の電気系BlockParamから上限値を取り出す（非電気系はfalse）
            // Extract limits from the 7 electric block params (non-electric returns false)
            return ElectricWireBlockParamResolver.TryGetWireParam(master.BlockParam, out maxWireConnectionCount, out maxWireLength);
        }

        /// <summary>
        /// 既存ブロック同士の接続可否を評価する。既接続・接続上限は受信済みワイヤー状態から内部で判定する
        /// Evaluate connecting two existing blocks; already-connected and full states are derived internally from received wire state
        /// </summary>
        public static ElectricWirePlacementJudgement Evaluate(BlockGameObject source, BlockGameObject target, int sourceMaxConnectionCount, int targetMaxConnectionCount, float sourceMaxWireLength, float targetMaxWireLength, float distance, ItemId wireItemId, IEnumerable<IItemStack> inventoryItems)
        {
            var alreadyConnected = IsAlreadyConnected(source, target);
            var anyConnectionFull = IsConnectionFull(source, sourceMaxConnectionCount) || IsConnectionFull(target, targetMaxConnectionCount);

            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, sourceMaxWireLength, targetMaxWireLength,
                alreadyConnected, anyConnectionFull,
                wireItemId, inventoryItems, ItemMaster.EmptyItemId);
        }

        /// <summary>
        /// 新設電柱への延長可否を評価する。新設側は未接続のため起点の状態のみ内部で判定する
        /// Evaluate extending to a newly placed pole; only the origin's state matters since the new pole has no connections
        /// </summary>
        public static ElectricWirePlacementJudgement EvaluateNewPole(BlockGameObject source, int sourceMaxConnectionCount, float sourceMaxWireLength, float poleMaxWireLength, float distance, ItemId wireItemId, ItemId poleItemId, IEnumerable<IItemStack> inventoryItems)
        {
            var sourceFull = IsConnectionFull(source, sourceMaxConnectionCount);

            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, sourceMaxWireLength, poleMaxWireLength,
                false, sourceFull,
                wireItemId, inventoryItems, poleItemId);
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
