using System;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 区画の値の流れを 1 View へ流す Presenter
    /// Generic presenter feeding section values into one view
    /// </summary>
    public sealed class DetailSectionPresenter<T> : BlockInventorySectionBase
    {
        private readonly IObservable<SectionContent<T>> _source;
        private readonly IDetailSectionView<T> _view;

        public override VisualElement Root => _view.Root;

        public DetailSectionPresenter(IObservable<SectionContent<T>> source, IDetailSectionView<T> view)
        {
            _source = source;
            _view = view;
        }

        public override void Attach(OpenBlockInventoryPanel panel)
        {
            AddSubscription(_source.Subscribe(Apply));

            #region Internal

            void Apply(SectionContent<T> content)
            {
                // 「取得できません」の行を持つのは文脈の区画だけ。ここへ届くのは結線の誤りなので隠して知らせる
                // Only the context section has the unavailable line; reaching here is a wiring mistake, so hide and report it
                if (content.TryGetUnavailable(out var fetchState)) Debug.LogError($"[DetailSectionPresenter] {typeof(T).Name}: this section has no unavailable line ({fetchState}); hidden");

                // 描いてから出す
                // Draw before showing; hide without drawing when there is no value
                var present = content.TryGetPresent(out var value);
                if (present) _view.Render(value);
                SetApplicable(present);
            }

            #endregion
        }
    }
}
