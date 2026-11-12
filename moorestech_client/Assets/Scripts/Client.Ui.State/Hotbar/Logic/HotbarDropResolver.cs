using System;

namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// ドラッグ元と落とし先の組を割当・入替・解除へ写す落としの規則の正本（Web hotbarDnd.ts と useHotbarDragSource の隙間の判定の写し）
    /// The single source of the drop rules mapping a source and target pair to assign, swap or clear (port of the Web hotbarDnd.ts plus the useHotbarDragSource gap check)
    /// </summary>
    public static class HotbarDropResolver
    {
        public static HotbarDropAction Resolve(HotbarDragSource source, HotbarDropTarget target)
        {
            switch (source)
            {
                // 枠へ落とすと割当。枠外・列の隙間は何もしない
                // Assigns only when dropped on a slot; outside and the row gap do nothing
                case BuildMenuEntryDragSource entry:
                    return target is HotbarSlotDropTarget slot
                        ? new HotbarAssignAction(slot.SlotIndex, entry.TargetId)
                        : HotbarNoDropAction.Instance;
                case HotbarSlotDragSource slotSource:
                    return ResolveFromSlot(slotSource.SlotIndex, target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, "未知のホットバーのドラッグ元");
            }

            #region Internal

            HotbarDropAction ResolveFromSlot(int sourceSlotIndex, HotbarDropTarget dropTarget)
            {
                switch (dropTarget)
                {
                    // 自分自身への落としは何も変えない
                    // A drop onto itself changes nothing
                    case HotbarSlotDropTarget slot:
                        return slot.SlotIndex == sourceSlotIndex
                            ? HotbarNoDropAction.Instance
                            : new HotbarSwapAction(sourceSlotIndex, slot.SlotIndex);
                    case OutsideHotbarDropTarget:
                        return new HotbarClearAction(sourceSlotIndex);
                    // 隙間はまだ HUD の中なので解除せず何もしない（Web useHotbarDragSource の data-hotbar-row）
                    // The gap is still inside the HUD, so it does nothing instead of clearing (Web useHotbarDragSource data-hotbar-row)
                    case HotbarRowGapDropTarget:
                        return HotbarNoDropAction.Instance;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dropTarget), dropTarget, "未知のホットバーの落とし先");
                }
            }

            #endregion
        }
    }
}
