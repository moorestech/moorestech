using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 1 Model を 1 View へ流す Presenter
    /// Section presenter feeding one model's value into one view; hidden when absent
    /// </summary>
    public sealed class DetailSectionPresenter<T> : IBlockInventorySection
    {
        private readonly IReadOnlyReactiveProperty<SectionContent<T>> _source;
        private readonly IDetailSectionView<T> _view;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view.Root;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public DetailSectionPresenter(IReadOnlyReactiveProperty<SectionContent<T>> source, IDetailSectionView<T> view)
        {
            _source = source;
            _view = view;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _source.Subscribe(Apply).AddTo(_subscriptions);

            #region Internal

            void Apply(SectionContent<T> content)
            {
                // 描いてから出す
                // Draw before showing; hide without drawing when there is no value
                if (content.IsPresent) _view.Render(content.Value);
                _isApplicable.Value = content.IsPresent;
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
