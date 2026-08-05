using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Core.Master;
using Game.UnlockState;
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
        private readonly IGameUnlockStateData _gameUnlockStateData;

        // 接続の起点ブロック。nullなら起点未選択状態
        // The connection origin block; null means no origin selected
        private BlockGameObject _sourceBlock;

        public ElectricWireConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore, IGameUnlockStateData gameUnlockStateData)
        {
            _gameUnlockStateData = gameUnlockStateData;

            var wirePreview = new ElectricWireExtendPreviewObject(mainCamera);
            var requestSender = new ElectricWireExtendRequestSender(blockGameObjectDataStore);
            // 解放済み電柱リストはEnable()で確定するため、構築時は空で始める
            // The unlocked pole list is settled in Enable(), so start empty at construction
            var poleSelection = new ElectricWirePoleSelection(new List<BlockId>());
            var pointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            var poleGhostPart = new ElectricWirePoleGhostPart(mainCamera, previewBlockController, localPlayerInventory.LocalPlayerInventory, pointCalculator);
            _context = new ElectricWireToolContext(mainCamera, previewBlockController, localPlayerInventory.LocalPlayerInventory, blockGameObjectDataStore, wirePreview, requestSender, poleSelection, poleGhostPart);

            _editMode = new ElectricWireEditMode(_context);
            _extendMode = new ElectricWireExtendMode(_context);
        }

        public override void Enable()
        {
            // 有効化のたびに起点選択をリセットし、進行中の応答を無効化し、解放済み電柱を再読込する
            // Reset the origin selection, invalidate any pending response and reload unlocked poles each time the tool is enabled
            _sourceBlock = null;
            _context.RequestSender.Invalidate();
            _context.PoleSelection.RefreshUnlockedPoles(_gameUnlockStateData);
        }

        protected override void ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged)
        {
            // 選択変化時のみ解放済み電柱を再読込する（毎tickのLINQ再構築を避ける）
            // Reload unlocked poles only on selection change to avoid rebuilding the LINQ query every tick
            if (isSelectionChanged) _context.PoleSelection.RefreshUnlockedPoles(_gameUnlockStateData);

            // 電柱種のスクロールサイクル・向きの回転入力を読む
            // Read the pole-type scroll cycle and rotate-key input
            _context.PoleSelection.UpdateInput();

            // 応答で確定した終点を取り込み、次の起点にする（チェーン）
            // Adopt the endpoint resolved from a response as the next origin (chaining)
            if (_context.RequestSender.TryConsumeEndpoint(out var endpointBlock)) _sourceBlock = endpointBlock;

            // 右クリックで起点を解除し、進行中の応答を無効化する
            // Release the origin on right click and invalidate any pending response
            if (_sourceBlock != null && InputManager.Playable.ScreenRightClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi())
            {
                _sourceBlock = null;
                _context.RequestSender.Invalidate();
            }

            // 起点未選択なら選択・切断・孤立設置、選択済みなら接続・延長を処理する
            // No origin: select, disconnect or isolated-place; with origin: connect or extend
            if (_sourceBlock == null)
            {
                _sourceBlock = _editMode.Update();
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
            _context.PoleGhostPart.SetNameLabelActive(false);
        }
    }
}
