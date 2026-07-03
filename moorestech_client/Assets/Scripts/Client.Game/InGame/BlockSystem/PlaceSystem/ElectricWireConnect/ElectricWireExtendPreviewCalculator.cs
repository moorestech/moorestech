using System.Collections.Generic;
using Client.Game.InGame.Block;
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
        /// ブロックが電気系（ワイヤー端点）かを判定し、最大ワイヤー長を返す
        /// Judge whether a block is electric (wire endpoint) and return its max wire length
        /// </summary>
        public static bool TryResolveMaxWireLength(BlockGameObject block, out float maxWireLength)
        {
            return TryResolveMaxWireLength(block.BlockMasterElement, out maxWireLength);
        }

        public static bool TryResolveMaxWireLength(BlockMasterElement master, out float maxWireLength)
        {
            // 7種の電気系BlockParamから最大長を取り出す（非電気系はfalse）
            // Extract max length from the 7 electric block params (non-electric returns false)
            if (ElectricWireBlockParamResolver.TryGetWireParam(master.BlockParam, out _, out maxWireLength)) return true;
            maxWireLength = 0f;
            return false;
        }

        /// <summary>
        /// 距離・両端の最大長・所持アイテムからサーバーと同じ接続可否を評価する
        /// Evaluate connection eligibility from distance, both endpoints' max length and held items
        /// </summary>
        public static ElectricWirePlacementJudgement Evaluate(float fromMaxWireLength, float toMaxWireLength, float distance, ItemId wireItemId, ItemId poleItemId, IEnumerable<IItemStack> inventoryItems)
        {
            // 既存接続状態はクライアントで確定できないためfalse固定（最終判定はサーバー権威）
            // Existing connection state is unknown on the client, so pass false (server is authoritative)
            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, fromMaxWireLength, toMaxWireLength,
                alreadyConnected: false, anyConnectionFull: false,
                wireItemId, inventoryItems, poleItemId);
        }
    }
}
