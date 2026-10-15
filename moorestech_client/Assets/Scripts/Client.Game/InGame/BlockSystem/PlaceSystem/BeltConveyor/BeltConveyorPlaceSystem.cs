using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Common.Debug;
using Game.Block.Interface;
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
        private readonly BeltConveyorPlaceRunBuilder _placeRunBuilder;

        private readonly PlacementHeightOffset _placementHeightOffset;

        private readonly CommonBlockPlaceDragState _dragState;

        private BlockDirection _currentBlockDirection = BlockDirection.North;
        private List<PlaceInfo> _currentPlaceInfos = new();

        public BeltConveyorPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, BlockGameObjectDataStore blockGameObjectDataStore, ILocalPlayerInventory localPlayerInventory, ConstructionWalletQuery constructionWalletQuery, PlacementHeightOffset placementHeightOffset)
        {
            _placementHeightOffset = placementHeightOffset;
            _dragState = new CommonBlockPlaceDragState(placementHeightOffset);
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
            _placeRunBuilder = new BeltConveyorPlaceRunBuilder(blockGameObjectDataStore, _dragState, new GroundConveyorTerrainFloor(placementHeightOffset));
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
            if (!DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) _previewBlockController.SetActive(false);

            // 連続設置状態をリセット。高さは持ち替えまで保つ
            // Reset the continuous placement state; the height stays until a block switch
            _dragState.ClearDrag();
            _currentPlaceInfos.Clear();
        }

        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            _currentBlockDirection = target.ResolveDirectionOnSelection(_currentBlockDirection, isSelectionChanged);
            PlacementHeightKeyInput.Apply(_placementHeightOffset);
            _currentBlockDirection = BeltConveyorInputControl.RotateDirection(_currentBlockDirection);
            var isSendable = GroundClickControl();
            PlaceBlockOnRelease();

            #region Internal

            void PlaceBlockOnRelease()
            {
                // 解放の畳みと送信可否は1つの入口で決める
                // Folding and sending on release are decided by a single entry
                if (!_dragState.TryConsumeSendableRelease(InputManager.Playable.ScreenLeftClick.GetKeyUp, isSendable, DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey))) return;

                // ベルトは電線を伴わないためワイヤー判定は常に許可
                // Belts never carry wires, so the wire check is always allowed
                TrySendOnClickRelease(_currentPlaceInfos, true);
            }

            // 戻り値はカーソル位置に送信できる設置列があるか
            // Returns whether the cursor has a sendable placement run
            bool GroundClickControl()
            {
                // 選択変更時に連続設置をリセット
                // Reset continuous placement when the selection changes
                if (isSelectionChanged) _dragState.DiscardForBlockChange(target.BlockGuid);

                _previewBlockController.SetActive(false);

                var holdingBlock = BeltConveyorHoldingBlock.Resolve(target.BlockId);
                var holdingBlockMaster = holdingBlock.BlockMaster;

                if (!TryGetFootprintGroundedRayHitBlockPosition(_mainCamera, _dragState.HeightOffset, _currentBlockDirection, holdingBlockMaster, feedback, out var placePoint, out var hitSurface)) { return false; }

                if (!IsPlaceableFromPlayer(placePoint)) { feedback.AddTooFar(); return false; }

                _previewBlockController.SetActive(true);

                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(placePoint, hitSurface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace);

                var (placeCauses, beltReasons, terrainClimbAbandoned) = UpdateCurrentPlaceInfos(placePoint, holdingBlock);

                _previewBlockController.SetPreview(_currentPlaceInfos, holdingBlockMaster, true);
                var blockGroundOverlapList = _previewBlockController.DetectGroundOverlaps();

                // 共通の重複理由を通知
                // Report shared overlap causes
                var cursorIndex = PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(_currentPlaceInfos, placeCauses, placePoint, blockGroundOverlapList, feedback);

                // 固有理由と地形断念を通知
                // Report belt-specific reasons and terrain-climb abandonment
                // -1は共用カーソル解決の既存契約。列外なら固有理由なしにする（型化は後続bd）
                // -1 is the shared cursor resolver's existing contract; use no reason outside the run (typed in follow-up bd)
                var cursorCellReason = cursorIndex < 0 ? BeltConveyorPlacementBlockReason.None : beltReasons[cursorIndex];
                BeltConveyorFeedbackReporter.Report(feedback, cursorCellReason, terrainClimbAbandoned);

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

                return true;
            }

            // 原因列と列全体の地形乗り越え断念を返す
            // Return cause columns and terrain-climb abandonment for the run
            (List<PlacementBlockCause> placeCauses, List<BeltConveyorPlacementBlockReason> beltReasons, bool terrainClimbAbandoned) UpdateCurrentPlaceInfos(Vector3Int placePoint, BeltConveyorHoldingBlock holdingBlock)
            {
                var dragStartPoint = _dragState.ResolveDragStartCell(placePoint);
                _currentPlaceInfos = _placeRunBuilder.Build(dragStartPoint, placePoint, _currentBlockDirection, holdingBlock, out var cellCauses, out var cellBeltReasons, out var climbAbandoned);
                return (cellCauses, cellBeltReasons, climbAbandoned);
            }

            #endregion
        }
    }
}
