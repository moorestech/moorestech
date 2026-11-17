using System;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 区画 Presenter の骨格。適用可否と購読を持ち、Detach の後始末（購読を切り適用可否を偽へ戻す）をここ 1 か所で行う
    /// Skeleton of a section presenter; owns applicability and subscriptions and does the Detach cleanup (unsubscribe, reset applicability) in this one place
    /// </summary>
    public abstract class BlockInventorySectionBase : IBlockInventorySection
    {
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public abstract VisualElement Root { get; }
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public abstract void Attach(OpenBlockInventoryPanel panel);

        public void Detach()
        {
            // 外れたら購読を切り、区画固有の状態を戻してから偽へ戻す（前のパネルの状態を次のパネルへ持ち越さない）
            // On detach unsubscribe, reset section-specific state, then reset to false (never carry the previous panel's state over)
            _subscriptions.Clear();
            ResetSectionStateOnDetach();
            _isApplicable.Value = false;
        }

        // 区画固有の状態（窓の選択等）を持つ区画だけが上書きする
        // Overridden only by sections holding their own state (window choice, etc.)
        protected virtual void ResetSectionStateOnDetach()
        {
        }

        protected void SetApplicable(bool applicable)
        {
            _isApplicable.Value = applicable;
        }

        // Attach で張った購読を Detach まで持つ
        // Holds a subscription made in Attach until Detach
        protected void AddSubscription(IDisposable subscription)
        {
            _subscriptions.Add(subscription);
        }
    }
}
