using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
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
        /// 受信済みワイヤー状態から起点⇔対象が既に接続済みかを判定する
        /// Judge whether origin and target are already connected, using received wire state
        /// </summary>
        public static bool IsAlreadyConnected(BlockGameObject blockA, BlockGameObject blockB)
        {
            // どちらか一方の接続先集合に相手が含まれていれば接続済み
            // Connected when either side's partner set contains the other
            if (blockA.TryGetComponent<ElectricWireStateChangeProcessor>(out var processorA) &&
                processorA.CurrentPartnerIds.Contains(blockB.BlockInstanceId)) return true;

            return blockB.TryGetComponent<ElectricWireStateChangeProcessor>(out var processorB) &&
                   processorB.CurrentPartnerIds.Contains(blockA.BlockInstanceId);
        }

        /// <summary>
        /// 受信済みワイヤー状態とマスタ上限から接続数が満杯かを判定する
        /// Judge whether the connection count is full, using received wire state and the master limit
        /// </summary>
        public static bool IsConnectionFull(BlockGameObject block, int maxWireConnectionCount)
        {
            return block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) &&
                   maxWireConnectionCount <= processor.CurrentPartnerIds.Count;
        }

        /// <summary>
        /// 距離・両端の最大長・接続状態・所持アイテムからサーバーと同じ接続可否を評価する
        /// Evaluate connection eligibility from distance, endpoint limits, connection state and held items
        /// </summary>
        public static ElectricWirePlacementJudgement Evaluate(float fromMaxWireLength, float toMaxWireLength, float distance, bool alreadyConnected, bool anyConnectionFull, ItemId wireItemId, ItemId poleItemId, IEnumerable<IItemStack> inventoryItems)
        {
            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, fromMaxWireLength, toMaxWireLength,
                alreadyConnected, anyConnectionFull,
                wireItemId, inventoryItems, poleItemId);
        }
    }
}
