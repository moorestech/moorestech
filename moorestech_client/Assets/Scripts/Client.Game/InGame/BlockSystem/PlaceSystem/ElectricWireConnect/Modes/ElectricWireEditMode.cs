using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Client.Game.InGame.Control;
using Client.Input;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Modes
{
    /// <summary>
    /// 起点未選択時の挙動。電気系ブロックの起点選択とワイヤークリック切断を処理する
    /// Behavior while no origin is selected: source selection on electric blocks and click-to-disconnect on wires
    /// </summary>
    public class ElectricWireEditMode
    {
        private readonly ElectricWireToolContext _context;

        public ElectricWireEditMode(ElectricWireToolContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 起点未選択の1フレーム更新。選択できた起点ブロックを返す（切断・未選択時はnull）
        /// One-frame update while no origin is selected; returns the newly selected origin block (null on disconnect or none)
        /// </summary>
        public BlockGameObject Update()
        {
            // 起点が無い状態では接続線プレビューは表示しない
            // No connection preview while there is no origin
            _context.WirePreview.SetActive(false);

            if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return null;
            if (UiPointerHitTest.IsPointerOverAnyUi()) return null;

            // ワイヤーを優先判定し、ヒットしたら切断する
            // Prioritize wires; disconnect when one is hit
            if (BlockClickDetectUtil.TryGetCursorOnElectricWire(out var wire))
            {
                Disconnect(wire);
                return null;
            }

            // 電気系ブロックにヒットしたら起点として選択する
            // Select as origin when an electric block is hit
            if (BlockClickDetectUtil.TryGetCursorOnBlock(out var block) &&
                ElectricWireExtendPreviewCalculator.TryResolveWireParam(block, out _, out _, out _))
            {
                return block;
            }

            return null;

            #region Internal

            void Disconnect(ElectricWireLineViewElement wireElement)
            {
                // 両端Idを座標解決し切断要求を送る
                // Resolve both endpoint InstanceIds to positions and send the disconnect request
                if (!_context.BlockDataStore.TryGetBlockGameObject(wireElement.FromId, out var fromBlock)) return;
                if (!_context.BlockDataStore.TryGetBlockGameObject(wireElement.ToId, out var toBlock)) return;

                var fromPos = fromBlock.BlockPosInfo.OriginalPos;
                var toPos = toBlock.BlockPosInfo.OriginalPos;
                ElectricWireExtendRequestSender.Disconnect(fromPos, toPos);
            }

            #endregion
        }
    }
}
