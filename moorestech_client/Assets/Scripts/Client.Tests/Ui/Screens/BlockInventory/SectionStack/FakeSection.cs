using System.Linq;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 区画の偽 Presenter（固定の name を持つ Root・外から書ける IsApplicable・Attach と Detach の回数）
    /// Fake section presenter (named Root, externally writable IsApplicable, Attach and Detach counters)
    /// </summary>
    public sealed class FakeSection : IBlockInventorySection
    {
        public readonly ReactiveProperty<bool> Applicable;
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }

        public VisualElement Root { get; private set; }
        public IReadOnlyReactiveProperty<bool> IsApplicable => Applicable;

        public FakeSection(string name, bool applicable)
        {
            Root = new VisualElement { name = name };
            Applicable = new ReactiveProperty<bool>(applicable);
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            AttachCount++;
        }

        public void Detach()
        {
            DetachCount++;
        }
    }

    public sealed class FakeSectionFactory : ISectionStackSectionFactory
    {
        private readonly bool _applicable;

        public SectionStackSlot Slot { get; private set; }
        public FakeSection Created { get; private set; }

        public FakeSectionFactory(SectionStackSlot slot, bool applicable)
        {
            Slot = slot;
            _applicable = applicable;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            Created = new FakeSection($"fake-{Slot}", _applicable);
            return Created;
        }

        // ItemGrid を除く全 Slot の偽の工場
        // Fake factories for every slot except ItemGrid
        public static ISectionStackSectionFactory[] AllSlots(bool applicable)
        {
            return System.Enum.GetValues(typeof(SectionStackSlot)).Cast<SectionStackSlot>()
                .Where(slot => slot != SectionStackSlot.ItemGrid)
                .Select(slot => (ISectionStackSectionFactory)new FakeSectionFactory(slot, applicable))
                .ToArray();
        }
    }
}
