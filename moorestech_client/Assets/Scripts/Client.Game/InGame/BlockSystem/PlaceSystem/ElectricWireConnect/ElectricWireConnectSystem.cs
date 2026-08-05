using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 電線アイテム所持中のツール。起点選択・接続・切断・レール式延長の4状態を統括する
    /// The tool active while holding a wire item; orchestrates origin-select, connect, disconnect and rail-style extend
    /// </summary>
    public class ElectricWireConnectSystem : PlaceSystemBase<ConnectToolPlacementTarget>
    {
        private readonly ElectricWireToolContext _context;
        private readonly ElectricWireEditMode _editMode;
        private readonly ElectricWireExtendMode _extendMode;

        // 接続の起点ブロック。nullなら起点未選択状態
        // The connection origin block; null means no origin selected
        private BlockGameObject _sourceBlock;

        public ElectricWireConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var wirePreview = new ElectricWireExtendPreviewObject(mainCamera);
            var requestSender = new ElectricWireExtendRequestSender(blockGameObjectDataStore);
            _context = new ElectricWireToolContext(mainCamera, previewBlockController, localPlayerInventory.LocalPlayerInventory, blockGameObjectDataStore, wirePreview, requestSender);
            _editMode = new ElectricWireEditMode(_context);
            _extendMode = new ElectricWireExtendMode(_context);
        }

        public override void Enable()
        {
            // 有効化のたびに起点選択をリセットし、進行中の応答を無効化する
            // Reset the origin selection and invalidate any pending response each time the tool is enabled
            _sourceBlock = null;
            _context.RequestSender.Invalidate();
        }

        protected override void ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged)
        {
            // 応答で確定した終点を取り込み、次の起点にする（チェーン）
            // Adopt the endpoint resolved from a response as the next origin (chaining)
            if (_context.RequestSender.TryConsumeEndpoint(out var endpointBlock)) _sourceBlock = endpointBlock;

            // 右クリックで起点を解除し、進行中の応答を無効化する
            // Release the origin on right click and invalidate any pending response
            if (_sourceBlock != null && InputManager.Playable.ScreenRightClick.GetKeyDown)
            {
                _sourceBlock = null;
                _context.RequestSender.Invalidate();
            }

            // 起点未選択なら選択・切断・孤立設置、選択済みなら接続・延長を処理する
            // No origin: select, disconnect or isolated-place; with origin: connect or extend
            if (_sourceBlock == null)
            {
                _sourceBlock = _editMode.Update(new PlaceSystemUpdateContext(target, isSelectionChanged));
                return;
            }

            _extendMode.Update(new PlaceSystemUpdateContext(target, isSelectionChanged), _sourceBlock);
        }

        public override void Disable()
        {
            // ツール切替時のみ起点を解除し、進行中の応答を無効化してプレビューを消す
            // Release the origin only on tool switch, invalidate any pending response and hide previews
            _sourceBlock = null;
            _context.RequestSender.Invalidate();
            _context.WirePreview.SetActive(false);
            _context.PreviewBlockController.SetActive(false);
        }
    }
}
