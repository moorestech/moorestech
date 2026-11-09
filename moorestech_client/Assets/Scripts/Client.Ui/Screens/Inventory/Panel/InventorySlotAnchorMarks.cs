using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Anchor;

namespace Client.Ui.Screens.Inventory.Panel
{
    /// <summary>
    /// 持ち物の枠の所持品のアンカーを、描き直しのたびに付け替える
    /// Re-applies the owned-item anchors on inventory slots on every redraw
    /// </summary>
    public sealed class InventorySlotAnchorMarks
    {
        private readonly Dictionary<int, string> _applied = new();

        public void Apply(IReadOnlyList<ItemSlot> slots, IReadOnlyDictionary<int, string> itemGuids)
        {
            for (var index = 0; index < slots.Count; index++)
            {
                // id は Mark と同じファイルで組む（TutorialAnchorCoverageTest が Mark のファイルの AnchorIds を拾う）
                // Build the id beside Mark (TutorialAnchorCoverageTest collects AnchorIds from files that call Mark)
                var wanted = itemGuids.TryGetValue(index, out var guid) ? AnchorIds.InventoryItem(guid) : null;
                _applied.TryGetValue(index, out var current);
                if (wanted == current) continue;

                // 古い id は印のクラスごと外す（枠に他のアンカーは付かない。Web の持ち物の枠のアンカーも inventoryItemAnchorId の 1 種だけ）
                // Remove the old id with the marker class (slots carry no other anchor; the Web inventory slot also has only inventoryItemAnchorId)
                if (current != null)
                {
                    slots[index].RemoveFromClassList(current);
                    slots[index].RemoveFromClassList(TutorialAnchor.MarkerClassName);
                    _applied.Remove(index);
                }
                if (wanted == null) continue;
                TutorialAnchor.Mark(slots[index], wanted);
                _applied[index] = wanted;
            }
        }

        public void Clear()
        {
            _applied.Clear();
        }
    }
}
