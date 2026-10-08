using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     回転・Q/E高さ・ドラッグ列を使ってBPを貼り付ける
    ///     Pastes blueprints with rotation, Q/E height and drag runs
    /// </summary>
    public class BlueprintPasteSystem : PlaceSystemBase<BlueprintPlacementTarget>
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlaceDragState _dragState;
        private BlueprintPastePreviewController _previewController;
        private BlueprintJsonObject _currentBlueprint;
        private int _rotationStep;
        private Vector3Int _footprintSize = Vector3Int.one;

        public override bool UsesPlacementHeight => true;

        public BlueprintPasteSystem(Camera mainCamera, ClientBlueprintLibrary library, BlockGameObjectDataStore blockGameObjectDataStore, PlacementHeightOffset heightOffset)
        {
            _mainCamera = mainCamera;
            _library = library;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _heightOffset = heightOffset;
            _dragState = new CommonBlockPlaceDragState(heightOffset);
        }

        public override void Enable()
        {
            _rotationStep = 0;
            _previewController ??= new BlueprintPastePreviewController(new GameObject("BlueprintPastePreview").transform);
        }

        protected override void ManualUpdate(BlueprintPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            if (isSelectionChanged)
            {
                _dragState.DiscardForSelectionChange();
                ResolveBlueprint(target.BlueprintGuid);
            }

            var isSendable = UpdatePastePreview(out var placements, out var placeableFlags);

            // 解放の畳みと送信可否は通常設置と同じ1つの入口で決める。押下と解放が同フレームでも押下登録の後に畳む
            // Folding and sending on release share the normal placement's single entry; a same-frame press is registered first
            if (!_dragState.TryConsumeSendableRelease(InputManager.Playable.ScreenLeftClick.GetKeyUp, isSendable, false)) return;

            // サーバーが再検証し部分成功を許すため、クライアントで置けると判定したものだけ送る
            // The server revalidates and allows partial success, so send only what the client judged placeable
            BlueprintPastePlaceSender.SendPlaceable(placements, placeableFlags);

            #region Internal

            // 戻り値はこのフレームで解放したら送信できる列があるか
            // Returns whether a release this frame has a run to send
            bool UpdatePastePreview(out List<BlueprintPlacementElement> runPlacements, out List<bool> flags)
            {
                runPlacements = null;
                flags = null;
                if (_currentBlueprint == null)
                {
                    _previewController.Hide();
                    return false;
                }

                PlacementHeightKeyInput.Apply(_heightOffset);
                if (InputManager.Playable.BlockPlaceRotation.GetKeyDown) Rotate();
                if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorAnchor))
                {
                    LogReleaseSkipped("cursor has no placement surface");
                    _previewController.Hide();
                    return false;
                }

                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(cursorAnchor, PlacementHitSurfaceKind.Ground);
                if (!PlaceSystemUtil.IsPlaceableFromPlayer(cursorAnchor))
                {
                    LogReleaseSkipped($"cursor {cursorAnchor} is too far from player");
                    _previewController.Hide();
                    feedback.AddTooFar();
                    return false;
                }

                runPlacements = BlueprintPasteRunBuilder.Build(_currentBlueprint, _dragState.ResolveDragStartCell(cursorAnchor), cursorAnchor, _footprintSize, _rotationStep);
                flags = runPlacements.Select(IsPlaceable).ToList();
                _previewController.UpdatePreview(runPlacements, flags);
                BlueprintPasteOverlapReasonReporter.Report(flags, feedback);
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp && UiPointerHitTest.IsPointerOverAnyUi())
                {
                    LogReleaseSkipped("pointer is over UI");
                    return false;
                }
                return true;
            }

            void LogReleaseSkipped(string reason)
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp && _dragState.IsDragging) Debug.Log($"[BlueprintPaste] release skipped: {reason}");
            }

            void ResolveBlueprint(Guid blueprintGuid)
            {
                _currentBlueprint = _library.TryGetBlueprint(blueprintGuid, out var blueprint) ? blueprint : null;
                if (_currentBlueprint == null)
                {
                    Debug.Log($"[BlueprintPaste] selected blueprint {blueprintGuid} is unavailable; preview and placement skipped");
                    return;
                }

                // マスタ欠損は解決時に一度だけ記録し、毎フレーム計算からは警告しない
                // Report missing master entries once at resolution, never on every preview frame
                var missing = _currentBlueprint.Blocks.Count(block => MasterHolder.BlockMaster.GetBlockIdOrNull(block.BlockGuid) == null);
                if (0 < missing) Debug.LogWarning($"[BlueprintPaste] blueprint {blueprintGuid} has {missing} blocks missing from the master; skipped");
                _footprintSize = BlueprintFootprintCalculator.CalcSize(_currentBlueprint, _rotationStep);
            }

            void Rotate()
            {
                _rotationStep = (_rotationStep + 1) % 4;
                _footprintSize = BlueprintFootprintCalculator.CalcSize(_currentBlueprint, _rotationStep);
            }

            bool IsPlaceable(BlueprintPlacementElement placement)
            {
                return !_blockGameObjectDataStore.IsOverlapPositionInfo(BlueprintPlacementElementUtil.ToPositionInfo(placement));
            }

            #endregion
        }

        public override bool TryCancelInProgressOperation()
        {
            if (!_dragState.IsDragging) return false;
            _dragState.ClearDrag();
            return true;
        }

        public override void Disable()
        {
            _dragState.ClearDrag();
            _previewController?.Hide();
            _currentBlueprint = null;
        }
    }
}
