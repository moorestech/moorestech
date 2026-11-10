using System.Collections.Generic;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Logic;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 解放物を種類ごとの節にまとめ、初出順に並べる（Web UnlockSections）
    /// Groups unlocks into per-kind sections in first-appearance order (Web UnlockSections)
    /// </summary>
    public sealed class ResearchUnlockSections : VisualElement
    {
        private readonly GameContentPresentation _content;

        public ResearchUnlockSections(GameContentPresentation content)
        {
            _content = content;
            pickingMode = PickingMode.Ignore;
        }

        public void Render(IReadOnlyList<UnlockEntry> entries)
        {
            // 解放物はノードごとに数件なので描くたびに組み直す
            // Unlocks are a handful per node, so rebuild on every render
            Clear();
            var containers = new Dictionary<ResearchUnlockSectionKind, VisualElement>();
            foreach (var entry in entries) ResearchUnlockEntryElements.AddTo(ContainerOf(ResearchUnlockSectionKinds.Of(entry)), entry, _content);

            #region Internal

            VisualElement ContainerOf(ResearchUnlockSectionKind kind)
            {
                if (containers.TryGetValue(kind, out var existing)) return existing;
                var section = new VisualElement { name = ResearchUnlockSectionKinds.Testid(kind) };
                section.AddToClassList("research-tree__section");
                var label = new LocalizedLabel();
                label.AddToClassList("research-tree__section-label");
                label.SetText(new UiText(ResearchUnlockSectionKinds.LabelKey(kind)));
                section.Add(label);
                Add(section);
                var container = section;
                if (ResearchUnlockSectionKinds.WrapsInSlotsRow(kind))
                {
                    container = new VisualElement();
                    container.AddToClassList("research-tree__slots");
                    section.Add(container);
                }
                containers.Add(kind, container);
                return container;
            }

            #endregion
        }
    }
}
