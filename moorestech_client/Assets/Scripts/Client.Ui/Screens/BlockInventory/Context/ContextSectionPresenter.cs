using Client.Ui.Screens.BlockInventory.SectionStack;
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
            // 外れたら偽へ戻す（前のパネルの適用可否を次のパネルへ持ち越さない）
            // Reset to false when detached so the previous panel's applicability is not carried over
            _subscriptions.Clear();
            _isApplicable.Value = false;
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
