using UniRx;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
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
    // - 箱・なぞり・ホバー入力を統括
    // - F長押し確定を処理
    // - ツールチップを更新
    // - Coordinate box, stroke and hover input
    // - Process F hold confirmation
    // - Update the tooltip
    public class DeleteObjectService
    {
        private readonly DeleteSelection _selection;
        private readonly DeleteBoxInput _box;
        private readonly DeleteHoldConfirm _hold;
        private readonly IMouseCursorTooltip _tooltip;
        private readonly TooltipOwner _tooltipOwner = new();
        private readonly List<TooltipLine> _lines = new();
        private bool _isStroking;
        private readonly DeleteResultFeedback _results = new();

        public DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender, ProgressBarState progressBar, Camera mainCamera, PlacementHeightOffset heightOffset, DeleteBoxTargetResolver boxTargetResolver)
        {
            _selection = new DeleteSelection(buildOperationHistory, restoreSender, new DeletePreviewResetRecovery());
            _box = new DeleteBoxInput(mainCamera, boxTargetResolver, heightOffset);
            _hold = new DeleteHoldConfirm(progressBar);
            // モード外の解放も受け、次の押下を受理できるようにする
            // Observe releases outside delete mode so the next press can start a fresh hold
            InputManager.Playable.Interact.OnGetKeyUp += () => _hold.Advance(false, 0f);
            _tooltip = tooltip;
            _selection.OnDeleteCompleted.Subscribe(_results.Receive);
            _selection.OnSelectionChanged.Subscribe(_ => _hold.SetSubject(_selection.HasSelection() ? DeleteHoldSubject.Selection(_selection.Revision) : DeleteHoldSubject.None));
        }

        public void Update(float deltaTime, bool isPointerOverUi)
        {
            _lines.Clear();

            // 固定カテゴリー内の最前面を取る（ストローク外は最前面）
            // Resolve the frontmost target in the stroke category, or overall outside a stroke
            var aim = DeleteTargetRaycaster.AimAt(_selection.AimFilter);
            var hovered = aim.Target;

            // 箱は終点確定のフレームに選択へ直接足す
            // The box adds to the selection itself on the frame the end point is confirmed
            _box.Update(_selection, _lines, isPointerOverUi);

            HandleStrokeStart();
            if (_isStroking) AddDenyLine(_selection.UpdateStroke(aim));
            var subject = _selection.UpdateHoverPreview(isPointerOverUi || _isStroking ? null : hovered, out var hoverReason);
            AddDenyLine(hoverReason);
            if (!_selection.HasSelection()) _hold.SetSubject(subject);
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp) EndStroke();

            UpdateHoldConfirm();

            if (_selection.HasSelection()) _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount, new[] { _selection.Count.ToString() }));
            _results.AppendTo(_lines);
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

            // 離した時点でカテゴリー固定を最前面へ戻す（選択は残る）
            // Releasing the button returns the category fix to frontmost (the selection stays)
            void EndStroke()
            {
                _isStroking = false;
                _selection.EndStroke();
            }

            void UpdateHoldConfirm()
            {
                if (!_hold.Advance(InputManager.Playable.Interact.GetKey, deltaTime)) return;
                var confirmedSubject = _selection.HasSelection() ? DeleteHoldSubject.Selection(_selection.Revision) : subject;
                _isStroking = false;
                if (!_selection.TryCommit(confirmedSubject, out var reason)) AddDenyLine(reason);
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
            return _selection.TryCancelSelection();
        }

        // 選択・箱・高さ・長押し・ツールチップを全解除
        // Clear selection, box, height, hold and tooltip
        public void ClearAll()
        {
            _box.Clear();
            _hold.Reset();
            _tooltip.Hide(_tooltipOwner);
            _isStroking = false;
            _selection.CancelSelection();
        }
    }
}
