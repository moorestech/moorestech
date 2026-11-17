using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の区画の Presenter
    /// Context section presenter; subscribes to the machine and context models and redoes the same decision on either
    /// </summary>
    public sealed class ContextSectionPresenter : BlockInventorySectionBase
    {
        private readonly BlockContextSectionsView _view;
        private readonly IBlockContextModel _context;
        private readonly IMachineSectionModel _machine;

        public override VisualElement Root => _view;

        public ContextSectionPresenter(BlockContextSectionsView view, IBlockContextModel context, IMachineSectionModel machine)
        {
            _view = view;
            _context = context;
            _machine = machine;
        }

        public override void Attach(OpenBlockInventoryPanel panel)
        {
            AddSubscription(_context.Current.Subscribe(_ => Refresh()));
            AddSubscription(_machine.Current.Subscribe(_ => Refresh()));
        }

        private void Refresh()
        {
            // 描いてから出す（出す判断は State。機械は専用の区画が出すので偽）
            // Draw before showing (the decision lives in State; machines show their own section, so false)
            var viewModel = _context.Current.Value;
            var shows = ContextSectionVisibility.Shows(_machine.Current.Value.IsPresent, viewModel);
            if (shows) _view.Render(viewModel);
            SetApplicable(shows);
        }
    }
}
