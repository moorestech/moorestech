using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
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
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly ConstructionWalletQuery _constructionWalletQuery;
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlacePointCalculator _blockPlacePointCalculator;
        private readonly ElectricWireAutoConnectPreview _autoConnectPreview;
        private readonly MapVeinAabbRegistry _veinAabbRegistry;
        private readonly IPlacementGroundFollower _groundFollower;

        private readonly CommonBlockPlaceDragState _dragState = new();

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private List<PlaceInfo> _currentPlaceInfos = new();

        public CommonBlockPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, IGameUnlockStateData gameUnlockStateData, ConstructionWalletQuery constructionWalletQuery, MapVeinAabbRegistry veinAabbRegistry, IPlacementGroundFollower groundFollower)
        {
            _mainCamera = mainCamera;
            _groundFollower = groundFollower;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
            _veinAabbRegistry = veinAabbRegistry;
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            _autoConnectPreview = new ElectricWireAutoConnectPreview(blockGameObjectDataStore, previewBlockController, gameUnlockStateData, constructionWalletQuery);
        }
        
        public override void Enable()
        {
            _dragState.ClearDrag();
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
            GroundClickControl();

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

            void GroundClickControl()
            {
                _dragState.SyncSelectedBlock(target.BlockId);

                //基本はプレビュー非表示
                _previewBlockController.SetActive(false);

                // ブロック設置用のrayが当たっているか、当たっていたら設置位置を取得する
                var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(target.BlockId);
                if (!TryGetRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, out var cursorCell, out var hitSurface)) { _autoConnectPreview.Hide(); EndDragWithoutPlacing(); return; }

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
                if (!IsPlaceableFromPlayer(placePoint)) { _autoConnectPreview.Hide(); feedback.AddTooFar(); EndDragWithoutPlacing(); return; }

                _previewBlockController.SetActive(true);

                //プレビュー表示と地面との接触を取得する
                //display preview and get collision with ground
                var blockGroundOverlapList = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);

                // この時点の不可原因は既存ブロックと地表欠落のみ。地面との接触反映とカーソルセルの理由集約を1回で行う
                // Existing blocks and missing ground are the only causes set by this point; apply ground overlaps and report the cursor cell's reasons in one call
                var cursorIndex = PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(_currentPlaceInfos, placeCauses, placePoint, blockGroundOverlapList, feedback);

                // 採掘機はドリルが鉱脈に重なるセルだけに制限する。素材チェックより前に落として枠を消費させない
                // Miners are restricted to cells where the drill overlaps a vein; drop them before the material check so they don't consume quota
                MinerVeinPlacementReporter.MarkOutsideVeinCellsAsNotPlaceable(_currentPlaceInfos, holdingBlockMaster, cursorIndex, _veinAabbRegistry, feedback);

                // 地面フィルタ後にアイテム数チェック（地面に埋まったブロックがアイテム枠を消費しないようにする）
                // Check item count after ground filtering (so ground-blocked cells don't consume item quota)
                ConstructionMaterialShortageReporter.ReportShortages(_currentPlaceInfos, target.BlockId, _constructionWalletQuery, _localPlayerInventory, feedback);
                ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(_currentPlaceInfos, target.BlockId, _constructionWalletQuery, _localPlayerInventory);

                // 各セルの自動接続を評価し表示更新。cursorIndexは上で解決済みのため再解決しない
                // Evaluate auto-connect per cell and update the preview; cursorIndex is already resolved above so it is not re-resolved
                var wirePlaceable = _autoConnectPreview.ApplyAutoConnect(_currentPlaceInfos, target.BlockId, _currentBlockDirection, _localPlayerInventory, cursorIndex, feedback);

                // 最終的なPlaceable状態でプレビュー色を更新
                // Update preview colors based on the final Placeable state
                _previewBlockController.UpdatePlaceableColors(_currentPlaceInfos);

                // 設置するブロックをサーバーに送信
                // send block place info to server
                PlaceBlock(wirePlaceable);
            }

            // 設置できないまま解放されたドラッグを畳む。残すと次フレームに古い開始点から列が伸びる
            // Folds a drag released with nothing to place; leaving it would extend a run from the stale start next frame
            void EndDragWithoutPlacing()
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp) _dragState.EndDrag();
            }

            void PlaceBlock(bool wirePlaceable)
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                // デバッグモード時は送信しない
                // Skip sending in debug mode
                if (DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) return;

                // マウスを離したので連続設置状態は解除する（押下未登録の解放はここで打ち切る）
                // Clear the continuous-placement state on mouse release (a release without a registered press stops here)
                if (!_dragState.EndDrag()) return;

                // 設置でワールドとインベントリが変わるため、自動接続の評価キャッシュを破棄する
                // Placement changes the world and inventory, so drop the auto-connect evaluation cache
                if (TrySendOnClickRelease(_currentPlaceInfos, wirePlaceable)) _autoConnectPreview.Hide();
            }

            #endregion
        }
    }
}
