using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// ベルトコンベアファミリー専用の1セル単位設置システム
    /// Dedicated per-cell placement system for belt-conveyor families
    /// </summary>
    public class BeltConveyorPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly BeltConveyorPlacePointCalculator _blockPlacePointCalculator;

        private readonly CommonBlockPlaceDragState _dragState = new();

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private bool? _isStartZDirection;
        private List<PlaceInfo> _currentPlaceInfos = new();

        public BeltConveyorPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _blockPlacePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore);
        }

        public override void Enable()
        {
            _dragState.SetClickStartHeightOffset(-1);
        }

        public override void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) _previewBlockController.SetActive(false);

            // 連続設置状態をリセット
            _dragState.ClearDrag();
            _isStartZDirection = null;
            _currentPlaceInfos.Clear();
        }

        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            _dragState.UpdateHeightOffsetByInput();
            _currentBlockDirection = BeltConveyorInputControl.RotateDirection(_currentBlockDirection);
            GroundClickControl(target, feedback);
        }

        private void GroundClickControl(BlockPlacementTarget target, PlacementFeedback feedback)
        {
            // ビルドメニューの選択ブロックが変わったら連続設置状態をリセット
            // Reset the continuous placement state when the build-menu selected block changes
            _dragState.SyncSelectedBlock(target.BlockId);

            //基本はプレビュー非表示
            _previewBlockController.SetActive(false);

            // ファミリー定義を解決し、非ファミリーブロックは対象外にする
            // Resolve the family definition and ignore non-family blocks
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(target.BlockId, out var family)) return;
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(family.StraightBlockId);

            // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
            if (!TryGetRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out _)) return;

            // 設置可能な距離かどうか
            if (!IsPlaceableFromPlayer(placePoint, PlaceableMaxDistance)) { feedback.AddTooFar(); return; }

            _previewBlockController.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(placePoint);

            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            SetCurrentPlaceInfo();

            var blockGroundOverlapList = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);

            // 各セルの不可原因はBlockCauseに立っている（既存重複・立体交差不能・坂ブロック欠落）
            // Each cell carries its block cause (existing overlap, impossible overpass, missing slope block)
            PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(_currentPlaceInfos, placePoint, blockGroundOverlapList, feedback);

            // 地面フィルタ後にアイテム数チェック（地面に埋まったエンティティがアイテム枠を消費しないようにする）
            // Check item count after ground filtering (so ground-blocked entities don't consume item quota)
            BeltConveyorCostPreviewMarker.MarkInsufficientEntitiesAsNotPlaceable(_currentPlaceInfos, _localPlayerInventory, feedback);

            // 最終的なPlaceable状態でプレビュー色を更新
            // Update preview colors based on the final Placeable state
            _previewBlockController.UpdatePlaceableColors(_currentPlaceInfos);

            // 設置するブロックをサーバーに送信
            // send block place info to server
            PlaceBlock();

            #region Internal

            void SetCurrentPlaceInfo()
            {
                var dragStartPoint = _dragState.ResolveDragStartPoint(placePoint);
                if (dragStartPoint == placePoint)
                {
                    _isStartZDirection = null;
                }
                else if (!_isStartZDirection.HasValue)
                {
                    _isStartZDirection = Mathf.Abs(placePoint.z - dragStartPoint.z) > Mathf.Abs(placePoint.x - dragStartPoint.x);
                }

                var cellInfos = _blockPlacePointCalculator.CalculatePoint(dragStartPoint, placePoint, _isStartZDirection ?? true, _currentBlockDirection, holdingBlockMaster);

                // セル列へ直線・坂ブロックを1対1で割り当てる
                // Assign straight and slope blocks to cells one-to-one
                _currentPlaceInfos = BeltConveyorCellBlockResolver.Resolve(cellInfos, family);
            }

            void PlaceBlock()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                // デバッグモード時は送信しない
                // Skip sending in debug mode
                if (DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) return;

                // マウスを離したので連続設置状態は解除する（設置有無に関わらず）
                // Clear the continuous-placement state on mouse release (regardless of whether we place)
                _dragState.EndDrag();

                BeltConveyorPlaceSender.TrySendOnClickRelease(_currentPlaceInfos);
            }

            #endregion
        }
    }
}
