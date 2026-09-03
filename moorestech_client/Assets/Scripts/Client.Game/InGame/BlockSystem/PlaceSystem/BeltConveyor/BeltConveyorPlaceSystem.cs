using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Construction;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceBlockProtocolSender;
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
        private readonly ConstructionWalletQuery _constructionWalletQuery;
        private readonly Camera _mainCamera;
        private readonly BeltConveyorPlacePointCalculator _blockPlacePointCalculator;

        private readonly CommonBlockPlaceDragState _dragState = new();

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private bool? _isStartZDirection;
        private List<PlaceInfo> _currentPlaceInfos = new();

        public BeltConveyorPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, ConstructionWalletQuery constructionWalletQuery)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
            _blockPlacePointCalculator = new BeltConveyorPlacePointCalculator(blockGameObjectDataStore);
        }

        public override void Enable()
        {
            _dragState.ClearDrag();
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
            if (!TryGetRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, out var placePoint, out var hitSurface)) return;

            // 設置可能な距離かどうか
            if (!IsPlaceableFromPlayer(placePoint)) { feedback.AddTooFar(); return; }

            _previewBlockController.SetActive(true);

            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(placePoint, hitSurface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace);

            //プレビュー表示と地面との接触を取得する
            //display preview and get collision with ground
            var (placeCauses, beltReasons) = UpdateCurrentPlaceInfos();

            _previewBlockController.SetPreview(_currentPlaceInfos, holdingBlockMaster);
            var blockGroundOverlapList = _previewBlockController.DetectGroundOverlaps();

            // 共有の不可原因（既存重複）は共通Reporterが積む
            // The shared block cause (existing overlap) is pushed by the shared reporter
            var cursorIndex = PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(_currentPlaceInfos, placeCauses, placePoint, blockGroundOverlapList, feedback);

            // ベルト固有の理由（立体交差不能・坂ブロック欠落）はベルト側が積む
            // Belt-specific reasons (impossible overpass, missing slope block) are pushed here on the belt side
            PushBeltReason();

            // 地面フィルタ後にアイテム数チェック（地面に埋まったエンティティがアイテム枠を消費しないようにする）
            // Check item count after ground filtering (so ground-blocked entities don't consume item quota)
            // ファミリー内は建設コストと設置数/1セットが一致する（マスタ検証済み）ので先頭の設置可セルを代表にする
            // Cost and placementsPerCost match within a family (validated at master load), so the first placeable cell is representative
            var representativeIndex = _currentPlaceInfos.FindIndex(info => info.Placeable);
            if (0 <= representativeIndex)
            {
                var representativeBlockId = _currentPlaceInfos[representativeIndex].BlockId;
                ConstructionMaterialShortageReporter.ReportShortages(_currentPlaceInfos, representativeBlockId, _constructionWalletQuery, _localPlayerInventory, feedback);
                ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(_currentPlaceInfos, representativeBlockId, _constructionWalletQuery, _localPlayerInventory);
            }

            // 最終的なPlaceable状態でプレビュー色を更新
            // Update preview colors based on the final Placeable state
            _previewBlockController.UpdatePlaceableColors(_currentPlaceInfos);

            // 設置するブロックをサーバーに送信
            // send block place info to server
            PlaceBlock();

            #region Internal

            void PushBeltReason()
            {
                if (cursorIndex < 0) return;

                var beltReason = beltReasons[cursorIndex];
                if (beltReason == BeltConveyorPlacementBlockReason.None) return;

                feedback.Add(new TooltipLine(BeltConveyorPlacementBlockReasonTooltipKey.ToKey(beltReason)));
            }

            // 設置点列を更新し、セル毎の共有原因とベルト固有理由の列を返す（PlaceInfo列と同じ添字）
            // Updates the placement point list and returns the per-cell shared cause and belt reason columns (indexed like the PlaceInfo list)
            (List<PlacementBlockCause> placeCauses, List<BeltConveyorPlacementBlockReason> beltReasons) UpdateCurrentPlaceInfos()
            {
                var dragStartPoint = _dragState.ResolveDragStartCell(placePoint);
                if (dragStartPoint == placePoint)
                {
                    _isStartZDirection = null;
                }
                else if (!_isStartZDirection.HasValue)
                {
                    _isStartZDirection = Mathf.Abs(placePoint.x - dragStartPoint.x) < Mathf.Abs(placePoint.z - dragStartPoint.z);
                }

                var cellInfos = _blockPlacePointCalculator.CalculatePoint(dragStartPoint, placePoint, _isStartZDirection ?? true, _currentBlockDirection, holdingBlockMaster, out var cellCauses, out var cellBeltReasons);

                // セル列へ直線・坂ブロックを1対1で割り当てる（坂欠落はベルト固有理由の列へ書き戻される）
                // Assign straight and slope blocks to cells one-to-one (a missing slope is written back into the belt reason column)
                _currentPlaceInfos = BeltConveyorCellBlockResolver.Resolve(cellInfos, family, cellBeltReasons);

                return (cellCauses, cellBeltReasons);
            }

            void PlaceBlock()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                // デバッグモード時は送信しない
                // Skip sending in debug mode
                if (DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) return;

                // マウスを離したので連続設置状態は解除する（押下未登録の解放はここで打ち切る）
                // Clear the continuous-placement state on mouse release (a release without a registered press stops here)
                if (!_dragState.EndDrag()) return;

                // ベルトは電線を伴わないためワイヤー判定は常に許可
                // Belts never carry wires, so the wire check is always allowed
                TrySendOnClickRelease(_currentPlaceInfos, true);
            }

            #endregion
        }
    }
}
