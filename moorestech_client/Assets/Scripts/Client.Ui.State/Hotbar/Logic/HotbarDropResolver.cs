using System;

namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// ドラッグ元と落とし先の組を割当・入替・解除へ写す。Web hotbarDnd.ts の写し
    /// Maps a drag source and drop target pair to assign, swap or clear; a port of the Web hotbarDnd.ts
    /// </summary>
    public static class HotbarDropResolver
    {
        public static HotbarDropAction Resolve(HotbarDragSource source, HotbarDropTarget target)
        {
            switch (source)
            {
                // メニューのエントリは枠へ落としたときだけ割当になる
                // A menu entry only assigns when it lands on a slot
                case BuildMenuEntryDragSource entry:
                    return target is HotbarSlotDropTarget slot
                        ? new HotbarAssignAction(slot.SlotIndex, entry.TargetId)
                        : HotbarNoDropAction.Instance;
                case HotbarSlotDragSource slotSource:
                    return ResolveFromSlot(slotSource.SlotIndex, target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, "未知のホットバーのドラッグ元");
            }
        }

        private static HotbarDropAction ResolveFromSlot(int sourceSlotIndex, HotbarDropTarget target)
        {
            switch (target)
            {
                // 自分自身への落としは何も変えない
                // A drop onto itself changes nothing
                case HotbarSlotDropTarget slot:
                    return slot.SlotIndex == sourceSlotIndex
                        ? HotbarNoDropAction.Instance
                        : new HotbarSwapAction(sourceSlotIndex, slot.SlotIndex);
                case OutsideHotbarDropTarget:
                    return new HotbarClearAction(sourceSlotIndex);
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "未知のホットバーの落とし先");
            }
        }
    }
}
