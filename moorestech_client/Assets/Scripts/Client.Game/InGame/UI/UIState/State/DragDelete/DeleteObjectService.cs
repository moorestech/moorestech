using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Input;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 破壊モードの削除インタラクション（箱・なぞり・ホバー・F長押し確定・ツールチップ）を毎フレーム束ねる司令塔
    // Per-frame conductor of destroy-mode interaction (box, stroke, hover, F hold-to-confirm, tooltip)
    public class DeleteObjectService
    {
        private readonly DeleteSelection _selection;
        private readonly DeleteBoxInput _box;
        private readonly DeleteHoldConfirm _hold;
        private readonly IMouseCursorTooltip _tooltip;
        private readonly TooltipOwner _tooltipOwner = new();
        private readonly List<TooltipLine> _lines = new();
        private bool _isStroking;

        public DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender, ProgressBarState progressBar, Camera mainCamera, PlacementHeightOffset heightOffset, DeleteBoxTargetResolver boxTargetResolver)
        {
            _selection = new DeleteSelection(buildOperationHistory, restoreSender);
            _box = new DeleteBoxInput(mainCamera, boxTargetResolver, heightOffset);
            _hold = new DeleteHoldConfirm(progressBar);
            _tooltip = tooltip;
        }

        public void Update(float deltaTime)
        {
            _lines.Clear();

            // 固定カテゴリー内の最前面を取る（ストローク外は最前面）
            // Resolve the frontmost target in the stroke category, or overall outside a stroke
            var aim = DeleteTargetRaycaster.AimAt(_selection.AimFilter);
            var hovered = aim.Target;
            var isPointerOverUi = UiPointerHitTest.IsPointerOverAnyUi();

            // 箱は終点確定のフレームに選択へ直接足す
            // The box adds to the selection itself on the frame the end point is confirmed
            _box.Update(_selection, _lines, isPointerOverUi);

            HandleStrokeStart();
            if (_isStroking) UpdateStroke();
            else UpdateHoverPreview();
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp) EndStroke();

            UpdateHoldConfirm();

            if (_selection.HasSelection()) _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount, new[] { _selection.Count.ToString() }));
            if (_lines.Count == 0) _tooltip.Hide(_tooltipOwner);
            else _tooltip.Show(_tooltipOwner, _lines.ToArray());

            #region Internal

            // 箱の物である押下（終点待ち・Shift）ではなぞりを始めない
            // A press the box owns (awaiting end / Shift) never starts a stroke
            void HandleStrokeStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (isPointerOverUi) return;
                if (_box.ClaimsLeftPress()) return;

                // ホバーの赤は選択側が引き継ぐ（追加時に再び赤になる）
                // The selection takes over the hover red (adding re-reds it)
                _selection.SetHoverPreview(null);
                _selection.BeginStroke();
                _isStroking = true;
            }

            void UpdateStroke()
            {
                if (!_selection.IsStrokeAlive()) return;
                if (hovered == null)
                {
                    AddDenyLine(aim.GetDragDenyReason());
                    return;
                }
                var outcome = _selection.TryAddTarget(hovered);
                if (!outcome.IsAdded) AddDenyLine(outcome.DenyReason);
            }

            // 離した時点でカテゴリー固定を最前面へ戻す（選択は残る）
            // Releasing the button returns the category fix to frontmost (the selection stays)
            void EndStroke()
            {
                _isStroking = false;
                _selection.EndStroke();
            }

            void UpdateHoverPreview()
            {
                _selection.SetHoverPreview(hovered);
                // 削除不可な対象は理由行だけ積む（理由なし拒否は何も出さない）
                // For a non-removable target only add the denial line (a reasonless denial adds nothing)
                if (hovered != null && !hovered.IsRemovable(out var reason)) AddDenyLine(reason);
            }

            // 長押しの対象: 選択があれば集合（Revision）、無ければ撤去可能なホバー1件、どちらも無ければ None
            // Hold subject: the selection (by revision) when present, else the single removable hovered target, else None
            void UpdateHoldConfirm()
            {
                var subject = DeleteHoldSubject.None;
                var hoverTarget = _selection.HoverPreview;
                if (_selection.HasSelection()) subject = DeleteHoldSubject.Selection(_selection.Revision);
                else if (hoverTarget != null && hoverTarget.IsRemovable(out _)) subject = DeleteHoldSubject.Hovered(hoverTarget.GetDeleteTargetKey());

                if (!_hold.Advance(InputManager.Playable.Interact.GetKey, subject, deltaTime)) return;

                // ホバー1件の暗黙選択: 足せなければ（撤去可否が直前に変わった）確定しない
                // Implicit single selection: if it cannot join (removability changed meanwhile) nothing is committed
                if (subject.Kind == DeleteHoldSubjectKind.Hovered)
                {
                    _selection.BeginStroke();
                    var outcome = _selection.TryAddTarget(hoverTarget);
                    if (!outcome.IsAdded)
                    {
                        Debug.Log($"[DragDelete] implicit hover commit refused: {outcome.Kind}");
                        _selection.SetHoverPreview(null);
                        return;
                    }
                }
                _selection.CommitDelete();
                _isStroking = false;
            }

            void AddDenyLine(LocalizationKey? denyReasonKey)
            {
                if (!denyReasonKey.HasValue) return;
                _lines.Add(new TooltipLine(denyReasonKey.Value));
            }

            #endregion
        }

        // ESC/右短押し: 長押しを戻し、終点待ちの箱 → 選択の全解除 の順に1段だけ畳む。畳む物が無ければ false（呼び出し側がモード終了を判断）
        // ESC / right short press: reset the hold, then fold the pending box, else clear the selection; false when nothing to fold (caller decides to exit)
        public bool TryCancel()
        {
            _hold.Cancel(InputManager.Playable.Interact.GetKey);
            if (_box.TryCancel()) return true;
            if (!_selection.HasSelection()) return false;
            _selection.CancelSelection();
            return true;
        }

        // モード離脱: 選択・箱（高さ含む）・長押し・ツールチップを全て片付ける
        // Mode exit: clear selection, box (including height), hold and tooltip
        public void ClearAll()
        {
            _selection.CancelSelection();
            _box.Clear();
            _hold.Reset();
            _tooltip.Hide(_tooltipOwner);
            _isStroking = false;
        }
    }
}
