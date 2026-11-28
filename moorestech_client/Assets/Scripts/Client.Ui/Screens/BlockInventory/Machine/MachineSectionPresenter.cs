using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の Presenter
    /// Machine section presenter: feeds machine and context models into the view; handles only mode-changing presses
    /// </summary>
    public sealed class MachineSectionPresenter : BlockInventorySectionBase
    {
        private readonly MachineSectionView _view;
        private readonly IMachineSectionModel _machine;
        private readonly IBlockContextModel _context;
        private readonly MachineSelectionModeState _mode = new();

        private readonly ReactiveProperty<IReadOnlyList<int>> _visibleItemSlots = new(Array.Empty<int>());

        internal IReadOnlyReactiveProperty<IReadOnlyList<int>> VisibleItemSlots => _visibleItemSlots;

        public override VisualElement Root => _view.Root;

        public MachineSectionPresenter(MachineSectionView view, IMachineSectionModel machine, IBlockContextModel context)
        {
            _view = view;
            _machine = machine;
            _context = context;
        }

        public override void Attach(OpenBlockInventoryPanel panel)
        {
            // 機械の通知は本体を、文脈の通知は文脈の 3 欄だけを描く（1 tick に本体を 2 回描かない。D-39）
            // Machine updates draw the body and context updates draw only the context entries (no double body render per tick; D-39)
            AddSubscription(_machine.Current.Subscribe(_ => Apply(panel)));
            AddSubscription(_context.Current.Subscribe(_view.RenderContext));
            AddSubscription(_view.OnChangeRecipeRequested.Subscribe(_ =>
            {
                _mode.OpenSelection();
                Apply(panel);
            }));

            // 行の押下は要求を覚えて描き直すだけ（送る判断はコマンド、送信とトーストは View。D-24・D-33）
            // A row press only remembers the request and redraws (the command decides sending; the view sends and toasts; D-24, D-33)
            AddSubscription(_view.OnRecipeRowPressed.Subscribe(recipeGuid =>
            {
                _mode.RememberRequest(recipeGuid);
                Apply(panel);
            }));
        }

        private void Apply(OpenBlockInventoryPanel panel)
        {
            // 描いてから出す
            // Draw before showing; hide without drawing when not a machine
            var present = _machine.Current.Value.TryGetPresent(out var machine) && machine.IsFor(panel);
            var showsSelection = present && _mode.ShowsSelection(machine);
            if (present) _view.Render(machine, showsSelection);

            // 共通グリッドへ、実際に描いた番号が変わったときだけ通知する
            // Notify the common grid only when the indices actually drawn change
            var visible = present && !showsSelection
                ? machine.SlotView.Inputs.Select(slot => slot.Index)
                    .Concat(machine.SlotView.Outputs.Select(slot => slot.Index)).Concat(machine.Bands.Module)
                : Array.Empty<int>();
            if (!_visibleItemSlots.Value.SequenceEqual(visible)) _visibleItemSlots.Value = visible.ToArray();
            SetApplicable(present);
        }
    }
}
