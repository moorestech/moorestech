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

            // 列: ホイール素通し・隙間の落とし先・チュートリアルの錨・calc() のつまみ
            // Row: wheel pass-through, gap drop target, tutorial anchor and calc() knobs
            _row = new VisualElement { name = "hotbar-grid" };
            _row.AddToClassList("hotbar-hud__row");
            WheelPassthroughMark.Mark(_row);
            UiDragDrop.MarkTarget<IHotbarDropReceiver>(_row, HotbarRowGapDropMark.Instance);
            TutorialAnchor.Mark(_row, AnchorIds.HotbarHud);
            _calcKnobs = new HotbarCalcKnobs(_row);
            Add(_row);
        }

        public void Render(HotbarViewModel viewModel)
        {
            // 退く間は帯ごと隠す（Web は null を返す。計画 D-10）
            // Hide the whole band while retreating (the Web returns null; plan D-10)
            style.display = viewModel.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            _row.EnableInClassList(SelectDisabledClass, !viewModel.SelectAccepted);

            if (_cells.Count != viewModel.Slots.Count) RebuildCells(viewModel.Slots.Count);
            for (var i = 0; i < _cells.Count; i++) _cells[i].Render(viewModel.Slots[i], viewModel.Selection.IsSlot(i));
        }

        // 枠の数は割当の長さに合わせる（通常は起動時に 1 回）。作り直したらタブのはみ出しを当て直す
        // Match the cell count to the assignment length (normally once at boot); re-apply the tab overhang after rebuilding
        private void RebuildCells(int count)
        {
            _row.Clear();
            _cells.Clear();
            for (var i = 0; i < count; i++)
            {
                var cell = new HotbarCell(i, _commands, _icons, _toaster);
                _cells.Add(cell);
                _row.Add(cell);
            }
            _calcKnobs.ApplyToRebuiltTabs();
        }
    }
}
