using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の区画の Presenter。機械の Model と文脈の Model を別々に購読し、どちらの通知でも同じ判断をやり直す（Web hasContextSections）
    /// Presenter of the context section; subscribes to the machine and context models separately and redoes the same decision on either (Web hasContextSections)
    /// </summary>
    public sealed class ContextSectionPresenter : IBlockInventorySection
    {
        private readonly BlockContextSectionsView _view;
        private readonly IBlockContextModel _context;
        private readonly IMachineSectionModel _machine;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public ContextSectionPresenter(BlockContextSectionsView view, IBlockContextModel context, IMachineSectionModel machine)
        {
            _view = view;
            _context = context;
            _machine = machine;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _context.Current.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            _machine.Current.Subscribe(_ => Refresh()).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }

        private void Refresh()
        {
            // 描いてから出す（出す判断は State。機械は専用の区画が出すので偽）
            // Draw before showing (the decision lives in State; machines show their own section, so false)
            var viewModel = _context.Current.Value;
            var shows = ContextSectionVisibility.Shows(_machine.Current.Value.IsPresent, viewModel);
            if (shows) _view.Render(viewModel);
            _isApplicable.Value = shows;
        }
    }
}
