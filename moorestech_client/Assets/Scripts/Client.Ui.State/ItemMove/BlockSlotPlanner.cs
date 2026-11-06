using System;
using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Item.Interface;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// ブロックスロットのクリック計画（Web shared/itemMove/blockSlotPlan の写し）
    /// Click plans for block slots (port of Web shared/itemMove/blockSlotPlan)
    /// </summary>
    public static class BlockSlotPlanner
    {
        // 左: Shift（空手・中身あり）は main へ配分 / 掴み中は全量置き / 中身ありは全量掴み
        // Left: Shift (empty-handed, filled) allocates into main / place all while holding / pick the whole stack
        public static IReadOnlyList<PlannedItemAction> PlanLeftClick(int index, SlotItem slot, bool shiftKey, int grabCount, IReadOnlyList<SlotItem> mainSlots, IItemStackLevelLookup stackLevels)
        {
            var blockRef = ItemMoveSlotRef.Block(index);
            // uGUI どおりサブ→メインだけ。ホットバーは宛先に含めない
            // Sub to main only, like uGUI; the hotbar is never a destination
            if (shiftKey && grabCount == 0 && !slot.IsEmpty) return DirectMovePlanner.PlanMoveActions(blockRef, slot, ItemMoveArea.Main, mainSlots, DirectMovePlanner.AllIndicesOf(mainSlots), stackLevels);
            if (0 < grabCount) return new[] { PlannedItemAction.BlockInventoryMoveItem(ItemMoveSlotRef.Grab, blockRef, grabCount) };
            if (!slot.IsEmpty) return new[] { PlannedItemAction.BlockInventoryMoveItem(blockRef, ItemMoveSlotRef.Grab, slot.Count) };
            return Array.Empty<PlannedItemAction>();
        }

        // 右: 掴み中は 1 個置き / 空手は split（半分の計算はコマンド側。古い個数に頼らない）
        // Right: place one while holding / split empty-handed (the command computes the half; no stale count)
        public static IReadOnlyList<PlannedItemAction> PlanRightClick(int index, SlotItem slot, int grabCount)
        {
            var blockRef = ItemMoveSlotRef.Block(index);
            if (0 < grabCount) return new[] { PlannedItemAction.BlockInventoryMoveItem(ItemMoveSlotRef.Grab, blockRef, 1) };
            if (slot.IsEmpty) return Array.Empty<PlannedItemAction>();
            return new[] { PlannedItemAction.BlockInventorySplit(blockRef) };
        }

        // ダブル: 集める先（持ち手かクリックしたスロットか）はコマンド側が現在の持ち手で決める
        // Double: the command picks the target (grab or clicked slot) from the current grab
        public static IReadOnlyList<PlannedItemAction> PlanDoubleClick(int index)
        {
            return new[] { PlannedItemAction.BlockInventoryCollect(ItemMoveSlotRef.Block(index)) };
        }
    }
}
