using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
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
    ///     Shift+左クリックで始点、次の左クリックで終点を取る箱選択の入力・セル解決・可視化。終点確定時に選択へ直接足す。高さは設置モードと共用のオフセット
    ///     Box-selection input (Shift+click start, click end), cell resolution and visuals; adds to the selection directly on end confirm. Height shares the placement offset
    /// </summary>
    public class DeleteBoxInput
    {
        private readonly Camera _mainCamera;
        private readonly DeleteBoxTargetResolver _resolver;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly DeleteBoxSelection _selection = new();
        private CellRangeBoxVisualizer _visualizer;
        private bool _isPressRegistered;

        public bool IsSelectingEnd => _selection.Phase == DeleteBoxPhase.SelectingEnd;

        public DeleteBoxInput(Camera mainCamera, DeleteBoxTargetResolver resolver, PlacementHeightOffset heightOffset)
        {
            _mainCamera = mainCamera;
            _resolver = resolver;
            _heightOffset = heightOffset;
        }

        // 終点待ち中かShift押下中は左押下は箱の物（この間なぞりは始まらない）
        // While awaiting the end point or holding Shift, the left press belongs to the box (no stroke starts)
        public bool ClaimsLeftPress()
        {
            return IsSelectingEnd || HybridInput.GetKey(KeyCode.LeftShift);
        }

        public void Update(DeleteSelection deleteSelection, List<TooltipLine> tooltipLines, bool isPointerOverUi)
        {
            PlacementHeightKeyInput.Apply(_heightOffset);
            var isShiftHeld = HybridInput.GetKey(KeyCode.LeftShift);
            var click = ConsumeClick();
            if (click == DeleteBoxClick.ReleasedOverUi) Debug.Log("[DeleteBox] click ignored: released over UI");
            var isClicked = click == DeleteBoxClick.Confirmed;

            if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorCell))
            {
                if (isClicked) Debug.Log("[DeleteBox] click refused: cursor has no placement surface");
                if (IsSelectingEnd) ShowStart(_selection.StartCell);
                else _visualizer?.HideAll();
                return;
            }

            if (IsSelectingEnd) UpdateSelectingEnd();
            else UpdateIdle();

            #region Internal

            // 箱の物である押下（ClaimsLeftPress）→UI外の解放を1クリックに畳む。UI上の解放は理由付きで捨てる
            // Fold a press the box owns (ClaimsLeftPress) and a release outside UI into one click; a release over UI is dropped with a reason
            DeleteBoxClick ConsumeClick()
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !isPointerOverUi && ClaimsLeftPress()) _isPressRegistered = true;
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp || !_isPressRegistered) return DeleteBoxClick.None;
                _isPressRegistered = false;
                return isPointerOverUi ? DeleteBoxClick.ReleasedOverUi : DeleteBoxClick.Confirmed;
            }

            void UpdateIdle()
            {
                // Shiftを押している間だけ始点候補を示す
                // Show the start candidate only while Shift is held
                if (isShiftHeld) ShowStart(cursorCell);
                else _visualizer?.HideAll();
                if (isClicked) _selection.SelectStart(cursorCell);
            }

            void UpdateSelectingEnd()
            {
                var (min, max) = CellRangeBox.Calc(_selection.StartCell, cursorCell);
                var targets = _resolver.Resolve(min, max);
                tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange, new[] { targets.Count.ToString() }));
                if (targets.Count == 0) tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxEmptyRange));
                GetVisualizer().ShowSelectingEnd(_selection.StartCell, cursorCell, min, max);
                if (!isClicked) return;

                // 空範囲は確定を拒み、ツールチップとログへ理由を残す
                // Refuse an empty range, leaving its reason in tooltip and log
                if (targets.Count == 0)
                {
                    Debug.Log($"[DeleteBox] end click refused: no removable targets in {min}-{max}");
                    return;
                }
                var added = deleteSelection.AddBoxTargets(targets);
                Debug.Log($"[DeleteBox] added={added} of {targets.Count} selected={deleteSelection.Count}");
                Clear();
            }

            // 始点を実際に表示するときだけ可視化オブジェクトを作る
            // Create the visualizer only when the start marker is actually shown
            void ShowStart(Vector3Int cell)
            {
                GetVisualizer().ShowSelectingStart(cell);
            }

            CellRangeBoxVisualizer GetVisualizer()
            {
                return _visualizer ??= new CellRangeBoxVisualizer("DeleteBox", MaterialConst.NotPlaceableColor);
            }

            #endregion
        }

        // 終点待ちなら箱を畳んで true（ESCの1段目）
        // Fold the pending box and return true (first ESC stage)
        public bool TryCancel()
        {
            if (!IsSelectingEnd) return false;
            Clear();
            return true;
        }

        // 局面・高さ・押下・表示を戻す（箱を畳む・確定後・モード離脱）
        // Reset phase, height, press and visuals (fold, after confirm, mode exit)
        public void Clear()
        {
            _selection.Clear();
            _resolver.ClearDiagnostics();
            _heightOffset.ResetToGround();
            _isPressRegistered = false;
            _visualizer?.HideAll();
        }
    }
}
