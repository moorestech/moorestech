using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 分割ドラッグの等分（Web ホストの SplitDragActionHandler と同じ: 一意で grab を受けられる行先に grab ÷ 行先数）
    /// Split-drag share (same as the Web host's SplitDragActionHandler: grab / destinations over unique slots that accept the grab)
    /// </summary>
    public static class SplitDragDistribution
    {
        public static SplitDragShare Plan(PlayerInventorySnapshot inventory, IReadOnlyList<PlayerSlotRef> slots)
        {
            var grab = inventory.Grab;
            var destinations = new List<PlayerSlotRef>();
            var stale = 0;
            foreach (var slot in slots)
            {
                if (destinations.Contains(slot)) continue;
                // 枠数が縮んだ後の古い番地は数えて除く（呼び手がログに出す）
                // Stale refs after the slot count shrank are counted and dropped (the caller logs them)
                if (!inventory.TryResolveSlot(slot.ToItemMoveSlotRef(), out var item))
                {
                    stale++;
                    continue;
                }
                // 受けられるのは空・同じアイテム・grab が空のとき（ホストの IsAllowedToAddWithRemain と同じ）
                // Accepts an empty slot, the same item, or any slot while grab is empty (host IsAllowedToAddWithRemain)
                if (!item.IsEmpty && !grab.IsEmpty && item.ItemId != grab.ItemId) continue;
                destinations.Add(slot);
            }
            // 配り先が無いかは Destinations.Count で見る（CountPerSlot の 0 は grab が配り先より少ない場合も含む）
            // Check Destinations.Count for no targets (CountPerSlot 0 also covers grab smaller than the target count)
            var countPerSlot = destinations.Count == 0 ? 0 : grab.Count / destinations.Count;
            return new SplitDragShare(destinations, countPerSlot, stale);
        }
    }
}
