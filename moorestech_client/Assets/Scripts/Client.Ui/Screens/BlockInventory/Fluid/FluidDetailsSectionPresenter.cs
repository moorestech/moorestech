using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体が 1 件以上あるときだけ出す
    /// Fluid-details section shown only with at least one fluid; machine-ness is ignored, as Web
    /// </summary>
    public sealed class FluidDetailsSectionPresenter : IBlockInventorySection
    {
        private readonly FluidInventoryContents _view;
        private readonly IBlockFluidSlotsModel _fluids;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public FluidDetailsSectionPresenter(FluidInventoryContents view, IBlockFluidSlotsModel fluids)
        {
            _view = view;
            _fluids = fluids;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _fluids.Current.Subscribe(fluids =>
            {
                if (0 < fluids.Slots.Count) _view.Render(fluids.Slots);
                _isApplicable.Value = 0 < fluids.Slots.Count;
            }).AddTo(_subscriptions);
        }

        public void Detach()
        {
            // 外れたら偽へ戻す（前のパネルの適用可否を次のパネルへ持ち越さない）
            // Reset to false when detached so the previous panel's applicability is not carried over
            _subscriptions.Clear();
            _isApplicable.Value = false;
        }
    }
}
