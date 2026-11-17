using System;
using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Selectors;
using Client.Ui.State.Items;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// 特殊族の本文・題の操作・区画の Presenter。selector が詳細を取れたときだけ View を描いて出し、取れなければ隠す。View と selector は詳細の型で結ばれ、常に出す本文は WholeSpecialDetailSelector と組む
    /// Presenter for a special body, title action or section; draws and shows the view only when the selector yields the detail, otherwise hides it; the view and selector are tied by the detail type, and always-shown bodies pair with WholeSpecialDetailSelector
    /// </summary>
    public sealed class SpecialSectionPresenter<TDetail> : BlockInventorySectionBase
    {
        private readonly ISpecialSectionRenderer<TDetail> _view;
        private readonly ISpecialDetailSelector<TDetail> _selector;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;

        public override VisualElement Root { get; }

        public SpecialSectionPresenter(VisualElement root, ISpecialSectionRenderer<TDetail> view, ISpecialDetailSelector<TDetail> selector, ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels)
        {
            Root = root;
            _view = view;
            _selector = selector;
            _model = model;
            _panels = panels;
        }

        public override void Attach(OpenBlockInventoryPanel panel)
        {
            AddSubscription(DetailStream()
                .Subscribe(pair =>
                {
                    // 出すかは描く前に selector で決め、描いてから IsApplicable で親へ知らせる（区画は自分の display を触らない。D-23）
                    // Decide showing with the selector before drawing, then report it via IsApplicable after drawing (sections never touch their own display. D-23)
                    var shows = _selector.TryGet(pair.detail, out var detail);
                    if (shows) _view.Render(pair.slots, detail);
                    SetApplicable(shows);
                }));

            #region Internal

            // 同じパネルの枠と特殊族の詳細の流れ。同じパネルの枠の変化と、パネルの組み直しの外で届く詳細（網の集約・ベルトの統計）の両方で描き直す
            // The same panel's slots joined with the special detail; redraw on both slot changes of the same panel and details arriving outside panel rebuilds (network totals, belt stats)
            IObservable<(IReadOnlyList<SlotItem> slots, SpecialBlockDetailViewModel detail)> DetailStream()
            {
                return _panels.Current
                    .Where(IsSamePanel)
                    .Select(current => ((OpenBlockInventoryPanel)current).ItemSlots)
                    .CombineLatest(_model.Current, (slots, detail) => (slots, detail));
            }

            bool IsSamePanel(BlockInventoryPanelState current)
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かない / alpha の BodyHost が同じフレームで Detach して新しい部分を作る（前例 alpha の SectionStackItemGridSection）
                return current is OpenBlockInventoryPanel open && open.IsSamePanel(panel);
            }

            #endregion
        }
    }
}
