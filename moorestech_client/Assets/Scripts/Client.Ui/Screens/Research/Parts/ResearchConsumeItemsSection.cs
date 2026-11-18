using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 消費アイテムの節（所持/必要・不足）
    /// Consume items section (owned/required)
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
            UiDisplay.SetShown(this, slots.Count != 0);
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
                UiDisplay.SetShown(_slots[i], shown);
                if (shown) _slots[i].SetContent(ContentOf(slots[i]));
            }

            #region Internal

            // 不足は枠の強調と赤で示す
            // Shortage shows as highlight and red
            ItemSlotContent ContentOf(ResearchConsumeSlotViewModel slot)
            {
                return SlotItemContent.ForMaterial(_content, slot.ItemId, MaterialTooltipKind.ResearchConsumeItem, slot.Allocated, slot.Required, slot.Lacking);
            }

            #endregion
        }
    }
}
