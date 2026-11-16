using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.Special.Belt;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Selectors;
using Client.Ui.State.Routing;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    // 特殊族の Presenter の組み立て。購読の源（詳細の Model・パネル・矢印キー・画面）を持つのはここだけで、View は受けない
    // Builds the special presenters; only this holds the subscription sources (detail model, panel, arrow keys, routing), never the views
    public sealed class SpecialPresenters
    {
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly IHorizontalArrowKeySource _arrowKeys;
        private readonly UiRoutingState _routing;

        public SpecialPresenters(ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels, IHorizontalArrowKeySource arrowKeys, UiRoutingState routing)
        {
            _model = model;
            _panels = panels;
            _arrowKeys = arrowKeys;
            _routing = routing;
        }

        // 本文・題の操作・区画（selector が詳細を取れたときだけ出す。View と selector は詳細の型で結ばれる。常に出す View は全体用の WholeSpecialDetailSelector と組む）
        // Body, title action and section (shown only when the selector yields the detail; the view and selector are tied by the detail type; always-shown views pair with WholeSpecialDetailSelector)
        public SpecialSectionPresenter<TDetail> Section<TView, TDetail>(TView view, ISpecialDetailSelector<TDetail> selector) where TView : VisualElement, ISpecialSectionRenderer<TDetail>
        {
            return new SpecialSectionPresenter<TDetail>(view, view, selector, _model, _panels);
        }

        public BeltThroughputSectionPresenter Belt(BeltThroughputSectionView view)
        {
            return new BeltThroughputSectionPresenter(view, _model, _arrowKeys, _routing, _panels);
        }
    }
}
