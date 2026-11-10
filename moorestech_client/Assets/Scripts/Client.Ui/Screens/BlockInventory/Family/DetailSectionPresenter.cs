using System;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 1 つの Model の値を 1 つの View へ流す区画の Presenter。値が無ければ隠す
    /// A section presenter feeding one model's value into one view; hidden when absent
    /// </summary>
    public sealed class DetailSectionPresenter<T> : IBlockInventorySection
    {
        private readonly IReadOnlyReactiveProperty<SectionContent<T>> _source;
        private readonly IDetailSectionView<T> _view;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private IDisposable _subscription;

        public VisualElement Root => _view.Root;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public DetailSectionPresenter(IReadOnlyReactiveProperty<SectionContent<T>> source, IDetailSectionView<T> view)
        {
            _source = source;
            _view = view;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            _subscription = _source.Subscribe(Apply);
        }

        public void Detach()
        {
            _subscription.Dispose();
        }

        private void Apply(SectionContent<T> content)
        {
            // 描いてから出す（値が無いときは描かずに隠す）
            // Draw before showing (hide without drawing when there is no value)
            if (content.IsPresent) _view.Render(content.Value);
            _isApplicable.Value = content.IsPresent;
        }
    }
}
