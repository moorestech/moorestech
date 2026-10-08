using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.Map.MapVein;
using Client.Game.InGame.Control;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceBlockProtocolSender;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class CommonBlockPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlacePointCalculator _blockPlacePointCalculator;
        private readonly ElectricWireAutoConnectPreview _autoConnectPreview;
        private readonly IPlacementGroundFollower _groundFollower;
        private readonly GearConnectPreview _gearConnectPreview;
        private readonly Evaluation.CommonBlockPlacementFeedbackPipeline _feedbackPipeline;
        private readonly ChainPlacementPreviewPart _chainPlacementPreviewPart;

        private readonly CommonBlockPlaceDragState _dragState;

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private List<PlaceInfo> _currentPlaceInfos = new();

        public CommonBlockPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, IGameUnlockStateData gameUnlockStateData, ConstructionWalletQuery constructionWalletQuery, MapVeinAabbRegistry veinAabbRegistry, IPlacementGroundFollower groundFollower, VeinRestrictedPlacementState veinRestrictedPlacementState, ChainPlacePreviewState chainPlacePreviewState, IChainGroundQuery chainGroundQuery, PlacementHeightOffset placementHeightOffset)
        {
            _dragState = new CommonBlockPlaceDragState(placementHeightOffset);
            _mainCamera = mainCamera;
            _groundFollower = groundFollower;
            _previewBlockController = previewBlockController;
            _gearConnectPreview = new GearConnectPreview(blockGameObjectDataStore);
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            _autoConnectPreview = new ElectricWireAutoConnectPreview(blockGameObjectDataStore, previewBlockController, gameUnlockStateData, constructionWalletQuery);
            _chainPlacementPreviewPart = new ChainPlacementPreviewPart(chainPlacePreviewState, _blockPlacePointCalculator, chainGroundQuery);
            _feedbackPipeline = new Evaluation.CommonBlockPlacementFeedbackPipeline(_previewBlockController, veinAabbRegistry, veinRestrictedPlacementState, chainPlacePreviewState, _blockPlacePointCalculator, chainGroundQuery, constructionWalletQuery, localPlayerInventory, _autoConnectPreview, _gearConnectPreview, _chainPlacementPreviewPart);
        }
        
        // Q/Eで動かす設置高さを読む系
        // A system that reads the placement height moved by Q/E
        public override bool UsesPlacementHeight => true;

        public override void Enable()
        {
            _dragState.ClearDrag();
        }
        // ドラッグ中の右短押し/Escは進行中のドラッグだけを畳み、建築モードには留まる
        // A right short press or Esc during a drag folds only that drag and stays in build mode
        public override bool TryCancelInProgressOperation()
        {
            if (!_dragState.IsDragging) return false;

            _dragState.EndDrag();
            return true;
        }

        public override void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey))
            {
                _previewBlockController.SetActive(false);
                HideConnectPreviews();
            }

            // 連続設置状態をリセット。高さは持ち替えまで保つ
            // Reset the continuous placement state; the height stays until a block switch
            _dragState.ClearDrag();
            _currentPlaceInfos.Clear();
        }

        // 電線と歯車のプレビューは必ず同時に畳む。片方だけ残すと実際の接続と食い違う線が残る
        // The wire and gear previews always fold together; leaving one behind strands lines that no longer match any connection
        private void HideConnectPreviews()
        {
            _autoConnectPreview.Hide();
            _gearConnectPreview.Hide();
            _chainPlacementPreviewPart.Hide();
        }
        
        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            _currentBlockDirection = target.ResolveDirectionOnSelection(_currentBlockDirection, isSelectionChanged);
            _dragState.UpdateHeightOffsetByInput();
            BlockDirectionControl();
            var isSendable = GroundClickControl(out var wirePlaceable);
            PlaceBlockOnRelease(isSendable, wirePlaceable);

            #region Internal

            void BlockDirectionControl()
            {
                if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    // 東西南北の向きを変更する
                    _currentBlockDirection = _currentBlockDirection.HorizonRotation();

                //TODo シフトはインプットマネージャーに入れる
                if (HybridInput.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    _currentBlockDirection = _currentBlockDirection.VerticalRotation();
            }

            // 戻り値はカーソル位置に送信できる設置列があるか
            // Returns whether the cursor has a sendable placement run
            bool GroundClickControl(out bool wirePlaceable)
            {
                wirePlaceable = false;
                if (isSelectionChanged) _dragState.DiscardForSelectionChange();

                //基本はプレビュー非表示
                _previewBlockController.SetActive(false);

                // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
                var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(target.BlockId);
                if (!TryGetRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, out var cursorCell, out var hitSurface)) { HideConnectPreviews(); return false; }

                // ドラッグ中は押下時の面種別で通す
                // A drag keeps the surface kind from its press
                var surfaceKind = _dragState.ResolveSurfaceKind(hitSurface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace);

                //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(cursorCell, surfaceKind);

                // 列の骨格は地形を混ぜない生のグリッドで決める。地形由来のYを混ぜると水平ドラッグがY軸列と判定される
                // The run skeleton is decided on the raw grid; a terrain-derived Y would make a horizontal drag look like a Y-axis run
                var run = CommonBlockPlacePointCalculator.CalculateRun(_dragState.ResolveDragStartCell(cursorCell), cursorCell, _currentBlockDirection, holdingBlockMaster);

                // Yの確定はこの1箇所だけで行う
                // This is the only place that finalizes Y
                _groundFollower.FollowGround(run, surfaceKind, holdingBlockMaster.BlockSize, _dragState.HeightOffset);

                // 重なり判定はY確定後に1度だけ行う
                // The overlap check runs exactly once, after Y is final
                _blockPlacePointCalculator.EvaluateExistingBlockCauses(run);

                _currentPlaceInfos = run.Cells;
                var placeCauses = run.BlockCauses;
                var placePoint = run.Cells[run.CursorIndex].Position;

                // 距離外なら理由のみ出しプレビュー無し
                // Beyond range, show only the reason and no preview
                if (!IsPlaceableFromPlayer(placePoint)) { HideConnectPreviews(); feedback.AddTooFar(); return false; }

                _previewBlockController.SetActive(true);

                // プレビュー投入とカーソル理由集約。地形との重なりは設置不可の理由にしない（ADR 0047）
                // Submit the preview and report the cursor reasons; terrain overlap never blocks placement (ADR 0047)
                var cursorIndex = NormalPlacementPreviewStep.Apply(_previewBlockController, _currentPlaceInfos, placeCauses, placePoint, holdingBlockMaster, feedback);

                // 鉱脈・資材・接続の評価を一箇所で行い、最終色を更新する
                // Evaluate veins, materials and connections in one place, then update final colors
                wirePlaceable = _feedbackPipeline.Apply(_currentPlaceInfos, holdingBlockMaster, target.BlockId, _currentBlockDirection, cursorIndex, surfaceKind, _dragState.HeightOffset, feedback);

                return true;
            }

            void PlaceBlockOnRelease(bool isSendable, bool wirePlaceable)
            {
                // 解放の畳みと送信可否は1つの入口で決める
                // Folding and sending on release are decided by a single entry
                if (!_dragState.TryConsumeSendableRelease(InputManager.Playable.ScreenLeftClick.GetKeyUp, isSendable, DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey))) return;

                // 設置でワールドとインベントリが変わるため、接続プレビューの評価キャッシュを破棄する
                // Placement changes the world and inventory, so drop the connect preview evaluation caches
                if (TrySendOnClickRelease(_currentPlaceInfos, wirePlaceable)) HideConnectPreviews();
            }

            #endregion
        }
    }
}
