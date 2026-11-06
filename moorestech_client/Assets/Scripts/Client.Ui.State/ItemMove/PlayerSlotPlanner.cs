using System;
using System.Collections.Generic;
using Client.Ui.State.Items;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// プレイヤースロット（main・equipment・grab）のクリック計画（Web shared/itemMove/playerSlotPlan の写し）
    /// Click plans for player slots (port of Web shared/itemMove/playerSlotPlan)
    /// </summary>
    public static class PlayerSlotPlanner
    {
        // 左: 掴み中の空スロットは分割ドラッグ開始 / 全量置き / Shift は配分 / 中身ありは全量掴み
        // Left: an empty slot while holding starts split-drag / place all / Shift allocates / pick the whole stack
        public static PlayerSlotActionPlan PlanLeftClick(ItemMoveSlotRef target, SlotItem slot, bool shiftKey, PlayerSlotContext context)
        {
            // 掴み中は中身の有無・同 ID・別 ID によらず全量置き（別 ID の入れ替えはコマンド側）
            // While holding, place all regardless of contents or id (the command swaps a different id)
            var grabCount = context.Inventory.Grab.Count;
            if (0 < grabCount)
            {
                if (!shiftKey && slot.IsEmpty) return new BeginSplitDragPlan(target);
                return ActionsOf(PlannedItemAction.InventoryMoveItem(ItemMoveSlotRef.Grab, target, grabCount));
            }

            // 空手: 空スロットは何もしない / Shift は配分 / それ以外は全量掴み
            // Empty-handed: an empty slot does nothing / Shift allocates / otherwise pick the whole stack
            if (slot.IsEmpty) return ItemActionsPlan.Empty;
            if (shiftKey) return PlanShiftMove(target, slot, context);
            return ActionsOf(PlannedItemAction.InventoryMoveItem(target, ItemMoveSlotRef.Grab, slot.Count));
        }

        // 右: 掴み中は 1 個置き / 空手は split（半分の計算はコマンド側）
        // Right: place one while holding / split empty-handed (the command computes the half)
        public static IReadOnlyList<PlannedItemAction> PlanRightClick(ItemMoveSlotRef target, SlotItem slot, int grabCount)
        {
            if (0 < grabCount) return new[] { PlannedItemAction.InventoryMoveItem(ItemMoveSlotRef.Grab, target, 1) };
            if (slot.IsEmpty) return Array.Empty<PlannedItemAction>();
            return new[] { PlannedItemAction.InventorySplit(target) };
        }

        // ダブル: 集める先はコマンド側が現在の持ち手で決める
        // Double: the command picks the target from the current grab
        public static IReadOnlyList<PlannedItemAction> PlanDoubleClick(ItemMoveSlotRef target)
        {
            return new[] { PlannedItemAction.InventoryCollect(target) };
        }

        // Shift: ブロック UI が開いていればブロックへ、閉じていれば装備からだけ main へ戻す（旧 main⇔hotbar の振り分けは廃止）
        // Shift: into the block while its UI is open; otherwise only equipment returns to main (the old main<->hotbar swap is gone)
        private static PlayerSlotActionPlan PlanShiftMove(ItemMoveSlotRef from, SlotItem slot, PlayerSlotContext context)
        {
            if (context is BlockOpenSlotContext open) return PlanShiftIntoBlock(from, slot, open);
            // silent-ok: ブロック閉時の main からの Shift は配分先が無い仕様（Web・uGUI と同じ） / 何も動かないことはプレイヤーが画面で見て分かる
            if (from.Area != ItemMoveArea.Equipment) return ItemActionsPlan.Empty;

            var mainSlots = context.Inventory.MainSlots;
            return new ItemActionsPlan(DirectMovePlanner.PlanMoveActions(from, slot, ItemMoveArea.Main, mainSlots, DirectMovePlanner.AllIndicesOf(mainSlots), context.StackLevels));
        }

        private static PlayerSlotActionPlan PlanShiftIntoBlock(ItemMoveSlotRef from, SlotItem slot, BlockOpenSlotContext context)
        {
            // 取得状態と「受け付ける枠が無い」は、理由を保って断りとして返す（呼び出し側が通知とログを出す）
            // Fetch states and "no accepting slot" come back as refusals with their reason (the caller notifies and logs)
            switch (context.Restriction)
            {
                case BlockInventoryOpenFailed _:
                    return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.InventoryOpenFailed);
                case BlockDetailLoading _:
                    return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.DetailLoading);
                case BoundBlockSlots boundSlots when boundSlots.CandidateIndices.Count == 0:
                    return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.NoAcceptingBlockSlot);
            }

            // 束縛があれば候補だけを宛先にする。move_item は枠固定の入れ替えで、束縛外はサーバーが無言で拒否するため
            // A bound restriction narrows destinations: move_item swaps a fixed slot and the server silently rejects unbound ones
            var candidateIndices = context.Restriction is BoundBlockSlots bound ? bound.CandidateIndices : DirectMovePlanner.AllIndicesOf(context.BlockItemSlots);
            return new ItemActionsPlan(DirectMovePlanner.PlanMoveActions(from, slot, ItemMoveArea.Block, context.BlockItemSlots, candidateIndices, context.StackLevels));
        }

        private static ItemActionsPlan ActionsOf(PlannedItemAction action)
        {
            return new ItemActionsPlan(new[] { action });
        }
    }
}
