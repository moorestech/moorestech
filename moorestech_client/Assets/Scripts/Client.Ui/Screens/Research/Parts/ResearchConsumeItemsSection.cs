using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 消費アイテムの節。所持/必要と不足を枠へ渡す（Web ResearchDetailPane の consume）
    /// Consume items section; passes owned/required and shortage to the slots (Web ResearchDetailPane consume)
    /// </summary>
    public sealed class ResearchConsumeItemsSection : VisualElement
    {
        private readonly GameContentPresentation _content;
        private readonly VisualElement _slotsRow;
        private readonly List<ItemSlot> _slots = new();

        public ResearchConsumeItemsSection(GameContentPresentation content)
        {
            _content = content;
            name = "research-consume-items";
            AddToClassList("research-tree__section");
            var label = new LocalizedLabel();
            label.AddToClassList("research-tree__section-label");
            label.SetText(new UiText(LocalizationKeys.Ui.Research.ConsumeItemsLabel));
            _slotsRow = new VisualElement();
            _slotsRow.AddToClassList("research-tree__slots");
            Add(label);
            Add(_slotsRow);
        }

        public void Render(IReadOnlyList<ResearchConsumeSlotViewModel> slots)
        {
            // 消費の無いノードは節ごと出さない
            // Nodes without consumption hide the whole section
            style.display = slots.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            while (_slots.Count < slots.Count)
            {
                var slot = new ItemSlot($"research-consume-slot-{_slots.Count}");
                slot.AddToClassList("research-tree__slot");
                _slotsRow.Add(slot);
                _slots.Add(slot);
            }
            for (var i = 0; i < _slots.Count; i++)
            {
                var shown = i < slots.Count;
                _slots[i].style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown) _slots[i].SetContent(ContentOf(slots[i]));
            }
        }

        // 不足は枠の強調と所持/必要の赤で示し、ツールチップは名前と所持・必要
        // Shortage shows as the frame highlight and red owned/required; the tooltip lists name, owned and required
        private ItemSlotContent ContentOf(ResearchConsumeSlotViewModel slot)
        {
            var tooltip = MaterialTooltipText.Create(MaterialTooltipKind.ResearchConsumeItem, _content.ItemName(slot.ItemId).Resolve(), slot.Required, slot.Allocated);
            var shortage = new ItemSlotShortage(slot.Allocated, slot.Required, slot.Lacking, tooltip);
            return new ItemSlotContent(_content.Item(slot.ItemId), null, new ItemSlotMarks(false, false, slot.Lacking), shortage, null, null);
        }
    }
}
