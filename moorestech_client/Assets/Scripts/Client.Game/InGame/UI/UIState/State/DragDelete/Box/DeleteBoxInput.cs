using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using Client.Input;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    internal enum DeleteBoxClick
    {
        None,
        Confirmed,
        ReleasedOverUi,
    }

    /// <summary>
    ///     Shift+左クリックで始点、次の左クリックで終点を取る箱選択の入力・セル解決・可視化。終点確定時に選択へ直接反映する（始点押下で Ctrl なら選択から外す）。高さは設置モードと共用のオフセット
    ///     Box-selection input (Shift+click start, click end), cell resolution and visuals; applies to the selection on end confirm (removes when Ctrl was held at the start press). Height shares the placement offset
    /// </summary>
    public class DeleteBoxInput
    {
        private readonly Camera _mainCamera;
        private readonly DeleteBoxTargetResolver _resolver;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly DeleteBoxSelection _selection = new();
        private readonly DeleteBoxIntentView _addView = DeleteBoxIntentView.CreateAdd();
        private readonly DeleteBoxIntentView _removeView = DeleteBoxIntentView.CreateRemove();
        private bool _isPressRegistered;
        private DeleteSelectionIntent _pressIntent = DeleteSelectionIntent.Add;

        private bool IsSelectingEnd => _selection.Phase == DeleteBoxPhase.SelectingEnd;

        internal DeleteBoxInput(Camera mainCamera, DeleteBoxTargetResolver resolver, PlacementHeightOffset heightOffset)
        {
            _mainCamera = mainCamera;
            _resolver = resolver;
            _heightOffset = heightOffset;
        }

        // 終点待ち中かShift押下中は左押下は箱の物（この間なぞりは始まらない）
        // While awaiting the end point or holding Shift, the left press belongs to the box (no stroke starts)
        internal bool ClaimsLeftPress()
        {
            return IsSelectingEnd || HybridInput.GetKey(KeyCode.LeftShift);
        }

        // heldIntent は呼び出し側がこのフレームに1回読んだ Ctrl の意図
        // heldIntent is the Ctrl intent the caller read once this frame
        internal void Update(DeleteSelection deleteSelection, List<TooltipLine> tooltipLines, bool isPointerOverUi, DeleteSelectionIntent heldIntent)
        {
            PlacementHeightKeyInput.Apply(_heightOffset);
            var isShiftHeld = HybridInput.GetKey(KeyCode.LeftShift);
            var click = ConsumeClick();
            if (click == DeleteBoxClick.ReleasedOverUi) Debug.Log("[DeleteBox] click ignored: released over UI");
            var isClicked = click == DeleteBoxClick.Confirmed;

            var hasCursorCell = PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorCell);
            if (!hasCursorCell)
            {
                if (isClicked) Debug.Log("[DeleteBox] click refused: cursor has no placement surface");
                if (!IsSelectingEnd) { HideViewsExcept(null); return; } // silent-ok: 始点未選択で指す面なし / 始点表示を隠して未選択を示す
                tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound));
                isClicked = false;
            }
            else if (IsSelectingEnd) _selection.SetEndCell(cursorCell);

            if (IsSelectingEnd) UpdateSelectingEnd();
            else UpdateIdle();

            #region Internal

            // 箱の物である押下（ClaimsLeftPress）→UI外の解放を1クリックに畳む。UI上の解放は理由付きで捨てる
            // Fold a press the box owns (ClaimsLeftPress) and a release outside UI into one click; a release over UI is dropped with a reason
            DeleteBoxClick ConsumeClick()
            {
                // 追加か除外かは押下の瞬間の Ctrl で決める
                // Ctrl at the press decides add or remove
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !isPointerOverUi && ClaimsLeftPress())
                {
                    _isPressRegistered = true;
                    _pressIntent = heldIntent;
                }
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp || !_isPressRegistered) return DeleteBoxClick.None;
                _isPressRegistered = false;
                return isPointerOverUi ? DeleteBoxClick.ReleasedOverUi : DeleteBoxClick.Confirmed;
            }

            void UpdateIdle()
            {
                // Shift中だけ始点候補を表示
                // Show the start candidate only while Shift is held
                if (isShiftHeld) ShowStart(cursorCell);
                else HideViewsExcept(null);
                if (isClicked)
                {
                    _selection.SelectStart(cursorCell, _pressIntent);
                    isClicked = false;
                    UpdateSelectingEnd();
                }
            }

            void UpdateSelectingEnd()
            {
                var (min, max) = CellRangeBox.Calc(_selection.StartCell, _selection.EndCell);
                var intent = _selection.Intent;
                var view = ViewFor(intent);
                var targets = _resolver.Resolve(min, max, deleteSelection, intent);
                tooltipLines.Add(new TooltipLine(view.CountKey, new[] { targets.Count.ToString() }));
                if (targets.Count == 0) tooltipLines.Add(new TooltipLine(view.EmptyRangeKey));
                HideViewsExcept(view);
                view.GetVisualizer().ShowSelectingEnd(_selection.StartCell, _selection.EndCell, min, max);
                if (!isClicked) return;

                // 空範囲拒否を表示とログへ記録
                // Record empty-range refusal in the tooltip and log
                if (targets.Count == 0)
                {
                    Debug.Log($"[DeleteBox] end click refused: no {view.TargetLabelForLog} targets in {min}-{max}");
                    return;
                }
                // 論理局面を先に閉じ、表示解除は例外時も行う
                // Close the logical phase first and release visuals even on failure
                _selection.Clear();
                _isPressRegistered = false;
                try { deleteSelection.ApplyBoxTargets(targets, intent); }
                finally { Clear(); }
            }

            // 押す前は今の Ctrl、押してから離すまでは押した瞬間の Ctrl の意図で始点を描く（確定と同じ意図を見せる）
            // Draw the start with the current Ctrl before the press, and with the press-time intent until release, matching what will be confirmed
            void ShowStart(Vector3Int cell)
            {
                var view = ViewFor(_isPressRegistered ? _pressIntent : heldIntent);
                HideViewsExcept(view);
                view.GetVisualizer().ShowSelectingStart(cell);
            }

            // 意図による表示の選び分けはここ1か所
            // The only place that picks presentation by intent
            DeleteBoxIntentView ViewFor(DeleteSelectionIntent intent) => intent == DeleteSelectionIntent.Remove ? _removeView : _addView;

            #endregion
        }

        // 終点待ちなら箱を畳んで true（ESCの1段目）
        // Fold the pending box and return true (first ESC stage)
        internal bool TryCancel()
        {
            _isPressRegistered = false;
            if (!IsSelectingEnd) return false;
            Clear();
            return true;
        }

        // 局面・高さ・押下・表示を戻す（箱を畳む・確定後・モード離脱）
        // Reset phase, height, press and visuals (fold, after confirm, mode exit)
        internal void Clear()
        {
            _selection.Clear();
            _resolver.ClearDiagnostics();
            _heightOffset.ResetToGround();
            _isPressRegistered = false;
            HideViewsExcept(null);
        }

        // 指定以外の意図の表示を隠す（null で両方）
        // Hide every intent's visuals except the given one (null hides both)
        private void HideViewsExcept(DeleteBoxIntentView kept)
        {
            if (!ReferenceEquals(_addView, kept)) _addView.Hide();
            if (!ReferenceEquals(_removeView, kept)) _removeView.Hide();
        }
    }
}
