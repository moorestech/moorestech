using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Hotbar.Parts;
using Client.Ui.State.Hotbar;
using Client.Ui.State.Hotbar.Logic;
using Client.Ui.State.Toast;
using Client.Ui.Foundation.Filter;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar
{
    /// <summary>
    /// ホットバーの帯・列・9 枠（Web features/hotbar/HotbarPanel）。Model を知らず Render で描く
    /// The hotbar band, row and nine slots (Web features/hotbar/HotbarPanel); knows no model and draws via Render
    /// </summary>
    public sealed class HotbarHudView : VisualElement
    {
        private const string SelectDisabledClass = "hotbar-hud__row--select-disabled";

        private readonly IHotbarCommands _commands;
        private readonly PlacementTargetIcons _icons;
        private readonly UiCommandResultToaster _toaster;
        private readonly VisualElement _row;
        private readonly PixelFilterGroup _rowGroup;
        private readonly HotbarCalcKnobs _calcKnobs;
        private readonly List<HotbarCell> _cells = new();

        public HotbarHudView(IHotbarCommands commands, PlacementTargetIcons icons, UiCommandResultToaster toaster)
        {
            _commands = commands;
            _icons = icons;
            _toaster = toaster;

            // 帯は画面幅の空箱なので拾わず、実 UI の列だけが拾う（Web .hotbarArea の pointer-events: none）
            // The band is an empty full-width box and does not pick; only the real-UI row does (Web .hotbarArea pointer-events: none)
            name = "hotbar-hud";
            pickingMode = PickingMode.Ignore;
            AddToClassList("hotbar-hud");

            // 列: 素通し・落とし先・錨・つまみ
            // Row: pass-through, drop target, anchor and knobs
            _row = new VisualElement { name = "hotbar-grid" };
            _row.AddToClassList("hotbar-hud__row");
            WheelPassthroughMark.Mark(_row);
            UiDragDrop.MarkTarget<IHotbarDropReceiver>(_row, HotbarRowGapDropMark.Instance);
            TutorialAnchor.Mark(_row, AnchorIds.HotbarHud);
            _calcKnobs = new HotbarCalcKnobs(_row);
            Add(_row);

            // 枠は群に入れ、受理外の減光を Web と同じく列の群に掛ける（A-222・A-226）。隙間の当たりは列自身
            // Cells go in a group so select-disabled dimming applies to the row group as on the Web (A-222, A-226); gaps still hit the row itself
            _rowGroup = new PixelFilterGroup(PixelFilterGroupFlow.Flow);
            _row.Add(_rowGroup);
        }

        // 退いた帯の中で掴んだままの押下を捨てる（残ると離した時に枠外ドロップとして割当が解除される）
        // Drop a press still captured inside the retreated band (otherwise releasing it fires an outside drop and clears the assignment)
        private void ReleaseCaptureInsideBand()
        {
            var capturing = panel?.GetCapturingElement(PointerId.mousePointerId) as VisualElement;
            if (capturing == null || !Contains(capturing)) return;
            capturing.ReleasePointer(PointerId.mousePointerId);
        }

        public void Render(HotbarViewModel viewModel)
        {
            // 退く間は帯ごと隠す
            // Hide the whole band while retreating
            style.display = viewModel.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!viewModel.Visible) ReleaseCaptureInsideBand();
            _row.EnableInClassList(SelectDisabledClass, !viewModel.SelectAccepted);

            if (_cells.Count != viewModel.Slots.Count) RebuildCells(viewModel.Slots.Count);
            for (var i = 0; i < _cells.Count; i++) _cells[i].Render(viewModel.Slots[i], viewModel.Selection.IsSlot(i));

            #region Internal

            // 枠の数は割当の長さに合わせる（通常は起動時に 1 回）。作り直したらタブのはみ出しを当て直す
            // Match the cell count to the assignment length (normally once at boot); re-apply the tab overhang after rebuilding
            void RebuildCells(int count)
            {
                _rowGroup.Clear();
                _cells.Clear();
                for (var i = 0; i < count; i++)
                {
                    var cell = new HotbarCell(i, _commands, _icons, _toaster);
                    _cells.Add(cell);
                    _rowGroup.Add(cell);
                }
                _calcKnobs.ApplyToRebuiltTabs();
            }

            #endregion
        }
    }
}
