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

                // 古い id を外す。他の id が残らなければ印のクラスも外れる
                // Remove the old id; the marker class goes too once no other id remains
                if (current != null)
                {
                    TutorialAnchor.Unmark(slots[index], current);
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
