using System;
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
using Game.Construction;
using Client.Game.InGame.UI.Inventory.Main;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
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
        private readonly ClientBlueprintPasteWorld _pasteWorld;
        private readonly ConstructionWalletQuery _walletQuery;
        private readonly ILocalPlayerInventory _inventory;
        private readonly BlueprintPasteLinePreview _linePreview = new();
        private Guid _currentBlueprintGuid;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly Camera _mainCamera;
        private readonly CommonBlockPlaceDragState _dragState;
        private BlueprintPastePreviewController _previewController;
        private BlueprintJsonObject _currentBlueprint;
        private int _rotationStep;
        private Vector3Int _footprintSize = Vector3Int.one;

        public override bool UsesPlacementHeight => true;

        public BlueprintPasteSystem(Camera mainCamera, ClientBlueprintLibrary library, BlockGameObjectDataStore blockGameObjectDataStore, PlacementHeightOffset heightOffset, ConstructionWalletQuery walletQuery, ILocalPlayerInventory inventory, PlacementTargetResolver targetResolver)
        {
            _mainCamera = mainCamera;
            _library = library;
            _pasteWorld = new ClientBlueprintPasteWorld(blockGameObjectDataStore, targetResolver);
            _walletQuery = walletQuery;
            _inventory = inventory;
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

            var isSendable = UpdatePastePreview(out var plan);

            // 解放の畳みと送信可否は通常設置と同じ1つの入口で決める。押下と解放が同フレームでも押下登録の後に畳む
            // Folding and sending on release share the normal placement's single entry; a same-frame press is registered first
            if (!_dragState.TryConsumeSendableRelease(InputManager.Playable.ScreenLeftClick.GetKeyUp, isSendable, false)) return;

            // サーバーが再検証し部分成功を許すため、クライアントで置けると判定したものだけ送る
            // The server revalidates and allows partial success, so send only what the client judged placeable
            BlueprintPastePlaceSender.Send(_currentBlueprintGuid, _rotationStep, plan);

            #region Internal

            // 戻り値はこのフレームで解放したら送信できる列があるか
            // Returns whether a release this frame has a run to send
            bool UpdatePastePreview(out BlueprintPastePlan runPlan)
            {
                runPlan = null;
                if (_currentBlueprint == null)
                {
                    HideAll();
                    return false;
                }

                PlacementHeightKeyInput.Apply(_heightOffset);
                if (InputManager.Playable.BlockPlaceRotation.GetKeyDown) Rotate();
                if (!BlueprintPasteOriginResolver.TryResolveCursorOrigin(_mainCamera, _footprintSize, _heightOffset.Value, out var cursorAnchor, out var surfaceKind))
                {
                    LogReleaseSkipped("cursor has no placement surface");
                    HideAll();
                    return false;
                }

                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(cursorAnchor, surfaceKind);
                if (!PlaceSystemUtil.IsPlaceableFromPlayer(cursorAnchor))
                {
                    LogReleaseSkipped($"cursor {cursorAnchor} is too far from player");
                    HideAll();
                    feedback.AddTooFar();
                    return false;
                }

                // 外接箱の列を共有プランナーへ渡し表示と送信の判定を統一する
                // Share one extent-run judgement between previews and sending
                var origins = BlueprintPasteRunBuilder.BuildOrigins(_dragState.ResolveDragStartCell(cursorAnchor), cursorAnchor, _footprintSize, _dragState.ResolveSurfaceKind(surfaceKind), _heightOffset.Value);
                runPlan = BlueprintPastePlanner.Plan(_currentBlueprint, origins, _rotationStep, _pasteWorld, _walletQuery, ConstructionMaterialAccounting.TallyHeld(_inventory));
                var ghosts = _previewController.UpdatePreview(runPlan);
                _linePreview.Show(runPlan, ghosts);
                BlueprintPasteFeedbackReporter.Report(runPlan, feedback);
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
                _currentBlueprintGuid = blueprintGuid;
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

            #endregion
        }

        private void HideAll()
        {
            _previewController?.Hide();
            _linePreview.Hide();
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
            HideAll();
            _currentBlueprint = null;
        }
    }
}
