using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Server.Protocol.PacketResponse;
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
        private readonly BlueprintPasteDragState _dragState;
        private BlueprintPastePreviewController _previewController;
        private BlueprintJsonObject _currentBlueprint;
        private int _rotationStep;
        private Vector3Int _footprintSize = Vector3Int.one;

        // 手編集セーブのnull設定は空設定として送る
        // Send null settings from hand-edited saves as empty settings
        private static readonly Dictionary<string, string> EmptySettings = new();

        public override bool UsesPlacementHeight => true;

        public BlueprintPasteSystem(Camera mainCamera, ClientBlueprintLibrary library, BlockGameObjectDataStore blockGameObjectDataStore, PlacementHeightOffset heightOffset)
        {
            _mainCamera = mainCamera;
            _library = library;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _heightOffset = heightOffset;
            _dragState = new BlueprintPasteDragState(heightOffset);
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

            // 解放はヒットの有無に関係なく先に畳む
            // Fold a release before hit testing so a sky release cannot keep a stale drag
            var releaseStartAnchor = _dragState.ResolveStartAnchor(Vector3Int.zero);
            var releaseHeightOffset = _heightOffset.Value;
            var isReleasedDrag = InputManager.Playable.ScreenLeftClick.GetKeyUp && _dragState.EndDrag();
            if (_currentBlueprint == null)
            {
                _previewController.Hide();
                return;
            }

            PlacementHeightKeyInput.Apply(_heightOffset);
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown) Rotate();
            if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, isReleasedDrag ? releaseHeightOffset : _heightOffset.Value, out var cursorAnchor))
            {
                if (isReleasedDrag) Debug.Log("[BlueprintPaste] release skipped: cursor has no placement surface");
                _previewController.Hide();
                return;
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(cursorAnchor, _heightOffset.Value);
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(cursorAnchor))
            {
                if (isReleasedDrag) Debug.Log($"[BlueprintPaste] release skipped: cursor {cursorAnchor} is too far from player");
                _previewController.Hide();
                feedback.AddTooFar();
                return;
            }

            var startAnchor = isReleasedDrag ? releaseStartAnchor : _dragState.ResolveStartAnchor(cursorAnchor);
            var placements = BlueprintPasteRunBuilder.Build(_currentBlueprint, startAnchor, cursorAnchor, _footprintSize, _rotationStep);
            var placeableFlags = placements.Select(IsPlaceable).ToList();
            _previewController.UpdatePreview(placements, placeableFlags);
            BlueprintPasteOverlapReasonReporter.Report(placeableFlags, feedback);

            // 解放時は設置可能な要素だけ送る
            // On release, send only placeable elements
            if (isReleasedDrag)
            {
                if (UiPointerHitTest.IsPointerOverAnyUi()) Debug.Log("[BlueprintPaste] release skipped: pointer is over UI");
                else SendPlace(placements, placeableFlags);
            }

            #region Internal

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
                if (missing > 0) Debug.LogWarning($"[BlueprintPaste] blueprint {blueprintGuid} has {missing} blocks missing from the master; skipped");
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

            void SendPlace(List<BlueprintPlacementElement> allPlacements, List<bool> flags)
            {
                var placeInfos = new List<PlaceInfo>();
                for (var i = 0; i < allPlacements.Count; i++)
                {
                    if (flags[i]) placeInfos.Add(ToPlaceInfo(allPlacements[i]));
                }

                if (placeInfos.Count == 0)
                {
                    Debug.Log("[BlueprintPaste] release skipped: no placeable blocks");
                    return;
                }

                PlaceBlockProtocolSender.SendPlaceBlockProtocol(placeInfos);
            }

            PlaceInfo ToPlaceInfo(BlueprintPlacementElement placement)
            {
                var createParams = (placement.Settings ?? EmptySettings)
                    .Select(kvp => new BlockCreateParam(kvp.Key, Encoding.UTF8.GetBytes(kvp.Value)))
                    .ToArray();

                return new PlaceInfo
                {
                    Position = placement.Position,
                    Direction = placement.Direction,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = placement.BlockId,
                    Placeable = true,
                    CreateParams = createParams,
                };
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
