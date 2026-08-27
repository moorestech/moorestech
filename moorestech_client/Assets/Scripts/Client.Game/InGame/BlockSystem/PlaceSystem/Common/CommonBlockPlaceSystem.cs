using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
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

        private readonly CommonBlockPlaceDragState _dragState = new();

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private List<PlaceInfo> _currentPlaceInfos = new();

        public CommonBlockPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, IGameUnlockStateData gameUnlockStateData, ConstructionWalletQuery constructionWalletQuery, MapVeinAabbRegistry veinAabbRegistry)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
            _veinAabbRegistry = veinAabbRegistry;
            _blockPlacePointCalculator = new CommonBlockPlacePointCalculator(blockGameObjectDataStore);
            _autoConnectPreview = new ElectricWireAutoConnectPreview(blockGameObjectDataStore, previewBlockController, gameUnlockStateData, constructionWalletQuery);
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
                if (!TryGetRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var hitSurface)) { _autoConnectPreview.Hide(); return; }

                // 地面ヒットのときだけ地形追従する
                // Follow the terrain only on a ground hit
                var isGroundHit = hitSurface == null;

                // 地面ヒットのYは地形最高点から決める
                // A ground hit decides Y from the terrain max height
                if (isGroundHit) placePoint = PlacementGroundCellResolver.ResolveCellFromGround(placePoint, _currentBlockDirection, holdingBlockMaster.BlockSize, _dragState.HeightOffset);

                // 距離外なら理由のみ出しプレビュー無し
                // Beyond range, show only the reason and no preview
                if (!IsPlaceableFromPlayer(placePoint)) { _autoConnectPreview.Hide(); feedback.AddTooFar(); return; }

                _previewBlockController.SetActive(true);

                //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(placePoint);

                //プレビュー表示と地面との接触を取得する
                //display preview and get collision with ground
                var placeCauses = UpdateCurrentPlaceInfos(placePoint, holdingBlockMaster);

                // 各セルのYを真下の地形へ追従させる
                // Make each cell's Y follow the terrain beneath it
                if (isGroundHit)
                {
                    PlacementGroundCellResolver.ApplyGroundCellY(_currentPlaceInfos, holdingBlockMaster.BlockSize, _dragState.HeightOffset);

                    // 重なり判定は追従後の位置で取り直す
                    // The overlap check is re-taken at the followed positions
                    _blockPlacePointCalculator.RecalculateExistingBlockCauses(_currentPlaceInfos, holdingBlockMaster, placeCauses);
                }

                var blockGroundOverlapList = _previewBlockController.SetPreviewAndGroundDetect(_currentPlaceInfos, holdingBlockMaster);

                // この時点の不可原因はExistingBlockのみ（CommonBlockPlacePointCalculator）。地面との接触反映とカーソルセルの理由集約を1回で行う
                // ExistingBlock is the only cause set by this point (CommonBlockPlacePointCalculator); apply ground overlaps and report the cursor cell's reasons in one call
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

            // 設置点列を更新し、セル毎の不可原因の列を返す（PlaceInfo列と同じ添字）
            // Updates the placement point list and returns the per-cell block cause column (indexed like the PlaceInfo list)
            List<PlacementBlockCause> UpdateCurrentPlaceInfos(Vector3Int placePoint, BlockMasterElement holdingBlockMaster)
            {
                _currentPlaceInfos = _blockPlacePointCalculator.CalculatePoint(_dragState.ResolveDragStartPoint(placePoint), placePoint, _currentBlockDirection, holdingBlockMaster, out var placeCauses);
                return placeCauses;
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
