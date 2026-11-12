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
    /// S4-alpha の本文・題の操作の口を実装する Presenter（常に出す View 用）。Attach から Detach まで、同じパネルの枠と特殊族の詳細の Model を購読して View へ流し、区画の出し入れを IsApplicable で知らせる
    /// Presenter implementing S4-alpha's body and title-action ports (for always-shown views); from Attach to Detach it streams the same panel's slots and the special detail model into the view and reports visibility through IsApplicable
    /// </summary>
    public sealed class SpecialDetailPresenter : IBlockInventorySection
    {
        private readonly ISpecialDetailRenderer _view;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IBlockInventoryPanelSource _panels;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root { get; private set; }
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;   // 型を狭める公開（前例 alpha の SectionStackItemGridSection）

        public SpecialDetailPresenter(VisualElement root, ISpecialDetailRenderer view, ISpecialBlockDetailModel model, IBlockInventoryPanelSource panels)
        {
            Root = root;
            _view = view;
            _model = model;
            _panels = panels;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            SpecialDetailStream.Of(_panels, _model, panel)
                .Subscribe(pair => Draw(pair.slots, pair.detail))
                .AddTo(_subscriptions);

            #region Internal

            void Draw(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
            {
                // 本文と題の操作は詳細の未着でも常に出す。描いてから IsApplicable で親へ知らせる（区画は自分の display を触らない。D-23）
                // Body and title action always show, even before the detail arrives; report it via IsApplicable after drawing (sections never touch their own display. D-23)
                _view.Render(itemSlots, detail);
                _isApplicable.Value = true;
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
