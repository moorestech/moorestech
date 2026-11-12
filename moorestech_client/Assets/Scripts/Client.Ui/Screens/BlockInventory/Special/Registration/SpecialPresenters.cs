using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Selectors;
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

        // 本文・題の操作（詳細の未着でも常に出す View）
        // Body and title action (views that always show, even before the detail arrives)
        public SpecialDetailPresenter Of<TView>(TView view) where TView : VisualElement, ISpecialDetailRenderer
        {
            return new SpecialDetailPresenter(view, view, _model, _panels);
        }

        // 区画（selector が詳細を取れたときだけ出す。View と selector は詳細の型で結ばれる）
        // Section (shown only when the selector yields the detail; the view and selector are tied by the detail type)
        public SpecialSectionPresenter<TDetail> Section<TView, TDetail>(TView view, ISpecialDetailSelector<TDetail> selector) where TView : VisualElement, ISpecialSectionRenderer<TDetail>
        {
            return new SpecialSectionPresenter<TDetail>(view, view, selector, _model, _panels);
        }

        public BeltThroughputSectionPresenter Belt(BeltThroughputSectionView view)
        {
            return new BeltThroughputSectionPresenter(view, _model, _arrowKeys);
        }
    }
}
