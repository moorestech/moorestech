using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行の区画。機械でなく詳細でない blockType で、液体が 1 件以上あるときだけ出す（Web SectionStackView の 13 番）
    /// The fluid-row section: shown for non-machine, non-detail block types with at least one fluid (Web SectionStackView slot 13)
    /// </summary>
    public sealed class FluidRowSectionPresenter : IBlockInventorySection
    {
        private readonly RowSectionStackFluid _fluid;
        private readonly FluidRowView _view;
        private readonly IBlockFluidSlotsModel _fluids;
        private readonly IMachineSectionModel _machine;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public FluidRowSectionPresenter(RowSectionStackFluid fluid, FluidRowView view, IBlockFluidSlotsModel fluids, IMachineSectionModel machine)
        {
            _fluid = fluid;
            _view = view;
            _fluids = fluids;
            _machine = machine;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 2 本を別々に購読し、どちらの通知でも同じ判断をやり直す（CombineLatest は Func の合成を要るので使わない）
            // Subscribe to both separately and redo the same decision on either (CombineLatest needs a Func selector)
            _fluids.Current.Subscribe(_ => Apply()).AddTo(_subscriptions);
            _machine.Current.Subscribe(_ => Apply()).AddTo(_subscriptions);
        }

        public void Detach()
        {
            _subscriptions.Clear();
        }

        private void Apply()
        {
            var fluids = _fluids.Current.Value;
            var shown = FluidRowVisibility.Shows(fluids, _machine.Current.Value.IsPresent);
            if (shown) _view.Render(fluids, _fluid.ShowProgress);
            _isApplicable.Value = shown;
        }
    }
}
