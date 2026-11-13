using Client.Input;
using Client.Input.ArrowKeys;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    /// <summary>
    /// ベルトの流量の区画の Presenter。詳細と ←/→ を Attach から Detach まで購読し、窓の選択（既定 180・端で止まる）と出し入れ（IsApplicable）を持つ（Web BeltThroughputSection の useState と keydown）
    /// Presenter of the belt throughput section; subscribes to details and ←/→ from Attach to Detach, owning the window choice (default 180, stops at the ends) and visibility (Web useState and keydown)
    /// </summary>
    public sealed class BeltThroughputSectionPresenter : IBlockInventorySection
    {
        private readonly BeltThroughputSectionView _view;
        private readonly ISpecialBlockDetailModel _model;
        private readonly IHorizontalArrowKeySource _arrowKeys;
        private readonly BeltWindowCursor _cursor = new();
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();
        private BeltThroughputDetail _belt;

        public VisualElement Root => _view;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public BeltThroughputSectionPresenter(BeltThroughputSectionView view, ISpecialBlockDetailModel model, IHorizontalArrowKeySource arrowKeys)
        {
            _view = view;
            _model = model;
            _arrowKeys = arrowKeys;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 詳細の変化と矢印の押下を Detach まで聞く
            // Listen to detail changes and arrow presses until Detach
            _model.Current.Subscribe(detail =>
            {
                _belt = detail.TryGetBelt(out var belt) ? belt : null;
                Draw();
            }).AddTo(_subscriptions);
            _arrowKeys.OnArrowPressed.Subscribe(OnArrowPressed).AddTo(_subscriptions);
        }

        public void Detach()
        {
            // 外れたら窓と適用可否を既定へ戻す
            // Reset window and applicability when detached
            _subscriptions.Clear();
            _cursor.Reset();
            _belt = null;
            _isApplicable.Value = false;
        }

        private void OnArrowPressed(HorizontalArrowKey key)
        {
            // 入力欄にフォーカスがある・ベルトが無い・表示されていないなら切り替えない（Web の isTextInputElement・readActiveLayer）
            // Do not switch while a text field has focus, without a belt or while hidden (Web isTextInputElement, readActiveLayer)
            // silent-ok: ベルト以外のブロック・閉じた画面・入力中の矢印はこの区画の操作ではない / 切り替わらないなら L1 と録画（R-12）で気づく
            if (_belt == null || UiInputExclusivity.IsTextInputFocused || !SpecialViewParts.IsDisplayedInHierarchy(_view)) return;
            _cursor.Step(_belt, key);
            Draw();
        }

        private void Draw()
        {
            // 窓が選べなければ出さない（Web の selected 無しの return null）。出し入れは親の SetItemShown（D-23）
            // Hide when no window can be selected (Web returns null without a selection); the parent's SetItemShown applies it (D-23)
            BeltThroughputWindow selected = null;
            _isApplicable.Value = _belt != null && BeltThroughputLogic.TrySelectBeltWindow(_belt.Windows, _cursor.SelectedTicks, out selected);
            if (_isApplicable.Value) _view.Render(BeltThroughputLogic.RowDisplay(selected), _belt.RecentSecondBars());
        }
    }
}
