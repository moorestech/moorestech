using System;

namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// 源と先の組を割当・入替・解除へ写す規則の正本
    /// Single source mapping source-target pairs to outcomes
    /// </summary>
    public static class HotbarDropResolver
    {
        public static HotbarDropAction Resolve(HotbarDragSource source, HotbarDropTarget target)
        {
            switch (source)
            {
                // 枠へ落とすと割当、他は何もしない
                // Assign only on a slot; others do nothing
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
