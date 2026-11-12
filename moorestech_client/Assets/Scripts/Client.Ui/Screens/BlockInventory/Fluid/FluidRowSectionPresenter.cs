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
    /// 液体の行の区画
    /// Fluid-row section shown for non-machine, non-detail block types with at least one fluid (Web SectionStackView slot 13)
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
            // 外れたら偽へ戻す（前のパネルの適用可否を次のパネルへ持ち越さない）
            // Reset to false when detached so the previous panel's applicability is not carried over
            _subscriptions.Clear();
            _isApplicable.Value = false;
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
