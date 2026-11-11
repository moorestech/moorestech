using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の Presenter の組み立て。購読の源（詳細の Model・パネル・矢印キー）を持つのはここだけで、View は受けない
    // Builds the special presenters; only this holds the subscription sources (detail model, panel, arrow keys), never the views
    public sealed class SpecialPresenters
    {
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly IHorizontalArrowKeySource _arrowKeys;

        public SpecialPresenters(ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels, IHorizontalArrowKeySource arrowKeys)
        {
            _model = model;
            _panels = panels;
            _arrowKeys = arrowKeys;
        }

        public SpecialDetailPresenter Of<TView>(TView view) where TView : VisualElement, ISpecialDetailRenderer
        {
            return new SpecialDetailPresenter(view, view, _model, _panels);
        }

        public BeltThroughputSectionPresenter Belt(BeltThroughputSectionView view)
        {
            return new BeltThroughputSectionPresenter(view, _model, _arrowKeys);
        }
    }
}
