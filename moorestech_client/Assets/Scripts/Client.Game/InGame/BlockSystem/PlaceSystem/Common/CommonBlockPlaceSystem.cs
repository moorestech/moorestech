using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.UnlockState;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class CommonBlockPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlacePointCalculator _blockPlacePointCalculator;
        private readonly ElectricWireAutoConnectPreview _autoConnectPreview;

        private readonly CommonBlockPlaceDragState _dragState = new();

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private List<PlaceInfo> _currentPlaceInfos = new();

        public CommonBlockPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, IGameUnlockStateData gameUnlockStateData)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            _autoConnectPreview = new ElectricWireAutoConnectPreview(blockGameObjectDataStore, previewBlockController, gameUnlockStateData);
        }
        
        public override void Enable()
        {
            _dragState.SetClickStartHeightOffset(-1);
        }
        public override void Disable()
        {
            // デバッグモード時はプレビューを維持
            // Keep preview in debug mode
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey))
            {
                _previewBlockController.SetActive(false);
                _autoConnectPreview.Hide();
            }

            // 連続設置状態をリセット
            _dragState.ClearDrag();
            _currentPlaceInfos.Clear();
        }
        
        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            ApplyPickedDirection();
            _dragState.UpdateHeightOffsetByInput();
            BlockDirectionControl();
            GroundClickControl(target, feedback);

            #region Internal

            void ApplyPickedDirection()
            {
                // スポイトでピックした向きを選択変化時に反映する
                // Apply the eyedropped block direction when the selection changes
                if (isSelectionChanged && target.PickedDirection.HasValue) _currentBlockDirection = target.PickedDirection.Value;
            }

            void BlockDirectionControl()
            {
                if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    // 東西南北の向きを変更する
                    _currentBlockDirection = _currentBlockDirection.HorizonRotation();
                
                //TODo シフトはインプットマネージャーに入れる
                if (HybridInput.GetKey(KeyCode.LeftShift) && InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                    _currentBlockDirection = _currentBlockDirection.VerticalRotation();
            }
            
            #endregion
        }
        
        
        private void GroundClickControl(BlockPlacementTarget target, PlacementFeedback feedback)
        {
            _dragState.SyncSelectedBlock(target.BlockId);

            //基本はプレビュー非表示
            _previewBlockController.SetActive(false);

            // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(target.BlockId);
            if (!TryGetRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var boundingBoxSurface)) { _autoConnectPreview.Hide(); return; }

            // 距離外なら理由のみ出しプレビュー無し
            // Beyond range, show only the reason and no preview
            if (!IsPlaceableFromPlayer(placePoint, PlaceableMaxDistance)) { _autoConnectPreview.Hide(); feedback.AddTooFar(); return; }

            _previewBlockController.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(placePoint);

            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            SetCurrentPlaceInfo();

            var blockGroundOverlapList = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);

            // この時点のPlaceable=falseは既存ブロックとの重なり（CommonBlockPlacePointCalculator）。地面との接触反映とカーソルセルの理由集約を1回で行う
            // Placeable=false at this point means overlap with an existing block (CommonBlockPlacePointCalculator); apply ground overlaps and report the cursor cell's reasons in one call
            var cursorIndex = PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(_currentPlaceInfos, placePoint, blockGroundOverlapList, feedback);

            // 地面フィルタ後にアイテム数チェック（地面に埋まったブロックがアイテム枠を消費しないようにする）
            // Check item count after ground filtering (so ground-blocked cells don't consume item quota)
            CommonBlockPlaceCostMarker.MarkInsufficientCellsAsNotPlaceable(_currentPlaceInfos, target.BlockId, _localPlayerInventory, feedback);

            // 各セルの自動接続を評価し表示更新。cursorIndexは上で解決済みのため再解決しない
            // Evaluate auto-connect per cell and update the preview; cursorIndex is already resolved above so it is not re-resolved
            var wirePlaceable = _autoConnectPreview.ApplyAutoConnect(_currentPlaceInfos, target.BlockId, _currentBlockDirection, _localPlayerInventory, cursorIndex, feedback);

            // 最終的なPlaceable状態でプレビュー色を更新
            // Update preview colors based on the final Placeable state
            _previewBlockController.UpdatePlaceableColors(_currentPlaceInfos);

            // 設置するブロックをサーバーに送信
            // send block place info to server
            PlaceBlock();

            #region Internal

            void SetCurrentPlaceInfo()
            {
                _currentPlaceInfos = _blockPlacePointCalculator.CalculatePoint(_dragState.ResolveDragStartPoint(placePoint), placePoint, _currentBlockDirection, holdingBlockMaster);
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

                // 設置でワールドとインベントリが変わるため、自動接続の評価キャッシュを破棄する
                // Placement changes the world and inventory, so drop the auto-connect evaluation cache
                if (CommonBlockPlaceSender.TrySendOnClickRelease(_currentPlaceInfos, wirePlaceable)) _autoConnectPreview.Hide();
            }

            #endregion
        }
    }
}
