using System;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 区画の値の流れを 1 View へ流す汎用 Presenter（族の詳細・液体の区画が共有する）。値が在るときだけ描いて出し、無ければ隠す
    /// Generic presenter feeding one stream of section values into one view (shared by family details and fluid sections); draws and shows only when present, hidden otherwise
    /// </summary>
    public sealed class DetailSectionPresenter<T> : IBlockInventorySection
    {
        private readonly IObservable<SectionContent<T>> _source;
        private readonly IDetailSectionView<T> _view;
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view.Root;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public DetailSectionPresenter(IObservable<SectionContent<T>> source, IDetailSectionView<T> view)
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
                // 「取得できません」の行を持つのは文脈の区画だけ。ここへ届くのは結線の誤りなので隠して知らせる
                // Only the context section has the unavailable line; reaching here is a wiring mistake, so hide and report it
                if (content.TryGetUnavailable(out var fetchState)) Debug.LogError($"[DetailSectionPresenter] {typeof(T).Name}: this section has no unavailable line ({fetchState}); hidden");

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
