using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 出さない区画（IsApplicable が常に偽）
    /// A section never shown: the blockType carries no matching fluid setting; IsApplicable is always false
    /// </summary>
    public sealed class NotApplicableSection : IBlockInventorySection
    {
        private static readonly IReadOnlyReactiveProperty<bool> Never = new ReactiveProperty<bool>(false);

        public VisualElement Root { get; } = new();
        public IReadOnlyReactiveProperty<bool> IsApplicable => Never;

        public void Attach(OpenBlockInventoryPanel panel)
        {
        }

        public void Detach()
        {
        }
    }
}
