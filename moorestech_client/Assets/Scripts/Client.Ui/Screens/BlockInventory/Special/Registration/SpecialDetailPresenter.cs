using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// S4-alpha の本文・題の操作・区画の口を 1 つで実装する Presenter。Attach から Detach まで、同じパネルの枠と特殊族の詳細の Model を購読して View へ流し、区画の出し入れを IsApplicable で知らせる
    /// Presenter implementing S4-alpha's body, title-action and section ports in one; from Attach to Detach it streams the same panel's slots and the special detail model into the view and reports section visibility through IsApplicable
    /// </summary>
    public sealed class SpecialDetailPresenter : IBlockInventorySection
    {
        private readonly ISpecialDetailRenderer _view;
        private readonly SpecialDetailSection _section;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root { get; private set; }
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;   // 型を狭める公開（前例 alpha の SectionStackItemGridSection）

        public SpecialDetailPresenter(VisualElement root, ISpecialDetailRenderer view, SpecialDetailSection section, ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels)
        {
            Root = root;
            _view = view;
            _section = section;
            _model = model;
            _panels = panels;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 同じパネルの枠の変化と、パネルの組み直しの外で届く詳細（網の集約・ベルトの統計）の両方で描き直す
            // Redraw on both slot changes of the same panel and details arriving outside panel rebuilds (network totals, belt stats)
            _panels.Current
                .Where(current => IsSame(current, panel))
                .Select(current => ((OpenBlockInventoryPanel)current).ItemSlots)
                .CombineLatest(_model.Current, (slots, detail) => (slots, detail))
                .Subscribe(pair => Draw(pair.slots, pair.detail))
                .AddTo(_subscriptions);

            #region Internal

            void Draw(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
            {
                // 出すかは描く前に State の表で決め、描いてから IsApplicable で親へ知らせる（区画は自分の display を触らない。D-23）
                // Decide showing from the State table before drawing, then report it via IsApplicable after drawing (sections never touch their own display. D-23)
                var shows = SpecialDetailVisibility.Shows(_section, detail);
                if (shows) _view.Render(itemSlots, detail);
                _isApplicable.Value = shows;
            }

            bool IsSame(BlockInventoryPanelState current, OpenBlockInventoryPanel attached)
            {
                // silent-ok: 別のパネルへ替わる途中の値は描かない / alpha の BodyHost が同じフレームで Detach して新しい部分を作る（前例 alpha の SectionStackItemGridSection）
                return current is OpenBlockInventoryPanel open && open.IsSamePanel(attached);
            }

            #endregion
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
