using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Construction;
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

        // 電柱種をサイクルできる間だけホイールを消費する。1種以下なら装備切替へ譲る
        // Consume the wheel only while pole types can be cycled; with one or none it yields to equipment switching
        public override bool OwnsWheelInput => _context.PoleSelection.CanCyclePoleType;

        public ElectricWireConnectSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, LocalPlayerInventoryController localPlayerInventory, BlockGameObjectDataStore blockGameObjectDataStore, IGameUnlockStateData gameUnlockStateData, ConstructionWalletQuery walletQuery)
        {
            _gameUnlockStateData = gameUnlockStateData;

            var wirePreview = new ElectricWireExtendPreviewObject();
            var requestSender = new ElectricWireExtendRequestSender(blockGameObjectDataStore);
            var poleSelection = new ElectricWirePoleSelection();
            var pointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            var poleGhostPart = new ElectricWirePoleGhostPart(mainCamera, previewBlockController, localPlayerInventory.LocalPlayerInventory, pointCalculator, walletQuery);
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

        protected override void ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            // 選択変化時のみ解放済み電柱を再読込する（毎tickのLINQ再構築を避ける）
            // Reload unlocked poles only on selection change to avoid rebuilding the LINQ query every tick
            if (isSelectionChanged) _context.PoleSelection.RefreshUnlockedPoles(_gameUnlockStateData);

            // 電柱種のスクロールサイクル・向きの回転入力を読む
            // Read the pole-type scroll cycle and rotate-key input
            _context.PoleSelection.UpdateInput();

            // 応答で確定した終点を次の起点にする（チェーン）。成功したのに終点を解決できなかった場合は
            // 終点がnullのまま代入され、起点が解除される。古い起点を残すとサーバーが張った線をもう一度張って電線を二重消費するため
            // Adopt the resolved endpoint as the next origin (chaining). On success without a resolved endpoint the null
            // is assigned and the origin is released: keeping the stale one would re-draw the server's wire and consume wire twice
            if (_context.RequestSender.TryConsumeOutcome(out var outcome) && outcome.IsSuccess) _sourceBlock = outcome.Endpoint;

            // 起点未選択なら選択・切断・孤立設置、選択済みなら接続・延長を処理する
            // No origin: select, disconnect or isolated-place; with origin: connect or extend
            if (_sourceBlock == null)
            {
                _sourceBlock = _editMode.Update(feedback);

                // 明示選択した起点は、応答待ちの孤立設置が後から返す終点に黙って上書きさせない
                // （上書きされるとプレイヤーが選んだブロックではなく新設電柱から配線され、電線が実消費される）
                // An explicitly selected origin must not be silently overwritten by a pending isolated placement's endpoint
                // (the wire would run from the new pole instead of the block the player picked, consuming wire for real)
                if (_sourceBlock != null) _context.RequestSender.Invalidate();
                return;
            }

            _extendMode.Update(new PlaceSystemUpdateContext(target, isSelectionChanged, feedback), _sourceBlock);
        }

        // 右短押しで起点解除と応答無効化（孤立設置含む）
        // A right short press releases the origin and invalidates any pending response, including an isolated placement
        public override bool TryCancelInProgressOperation()
        {
            var hadInProgress = _sourceBlock != null || _context.RequestSender.IsAwaitingResponse;
            _sourceBlock = null;
            _context.RequestSender.Invalidate();
            return hadInProgress;
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
