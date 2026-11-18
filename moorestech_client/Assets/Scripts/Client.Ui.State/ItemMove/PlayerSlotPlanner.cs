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
                return ActionsOf(PlannedItemAction.MoveItem(ItemMoveSlotRef.Grab, target, grabCount));
            }

            // 空手: 空スロットは何もしない / Shift は配分 / それ以外は全量掴み
            // Empty-handed: an empty slot does nothing / Shift allocates / otherwise pick the whole stack
            if (slot.IsEmpty) return ItemActionsPlan.Empty;
            if (shiftKey) return PlanShiftMove(target, slot);
            return ActionsOf(PlannedItemAction.MoveItem(target, ItemMoveSlotRef.Grab, slot.Count));

            #region Internal

            // Shift: ブロック UI が開いていればブロックへ、閉じていれば装備からだけ main へ戻す（旧 main⇔hotbar の振り分けは廃止）
            // Shift: into the block while its UI is open; otherwise only equipment returns to main (the old main<->hotbar swap is gone)
            PlayerSlotActionPlan PlanShiftMove(ItemMoveSlotRef from, SlotItem source)
            {
                if (context is BlockOpenSlotContext openContext) return PlanShiftIntoBlock(from, source, openContext);
                // silent-ok: ブロック閉時の main からの Shift は配分先が無い仕様（Web・uGUI と同じ） / 何も動かないことはプレイヤーが画面で見て分かる
                if (from.Area != ItemMoveArea.Equipment) return ItemActionsPlan.Empty;

                var mainSlots = context.Inventory.MainSlots;
                return PlanAllocation(from, source, ItemMoveArea.Main, mainSlots, DirectMovePlanner.AllIndicesOf(mainSlots));
            }

            PlayerSlotActionPlan PlanShiftIntoBlock(ItemMoveSlotRef from, SlotItem source, BlockOpenSlotContext openContext)
            {
                // 取得状態・受け付ける枠が無い・古い候補は送らずに理由を返す（呼び出し側が通知とログを出す）
                // Fetch states, no accepting slot and stale candidates return unsent with a reason (the caller notifies and logs)
                IReadOnlyList<int> candidateIndices;
                switch (openContext.Restriction)
                {
                    case UnrestrictedBlockSlots _:
                        // 枠 0 のブロックは宛先が無いのであって満杯ではない
                        // A block with 0 slots has no destination rather than being full
                        if (openContext.BlockItemSlots.Count == 0) return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.NoAcceptingBlockSlot);
                        candidateIndices = DirectMovePlanner.AllIndicesOf(openContext.BlockItemSlots);
                        break;
                    case BoundBlockSlots boundSlots:
                        // 束縛があれば候補だけを宛先にする。move_item は枠固定の入れ替えで、束縛外はサーバーが無言で拒否するため
                        // A bound restriction narrows destinations: move_item swaps a fixed slot and the server silently rejects unbound ones
                        if (boundSlots.CandidateIndices.Count == 0) return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.NoAcceptingBlockSlot);
                        if (TryFindOutOfRangeCandidate(boundSlots.CandidateIndices, openContext.BlockItemSlots.Count, out var staleCandidate)) return new StaleSlotIgnoredPlan(ItemMoveSlotRef.Block(staleCandidate));
                        candidateIndices = boundSlots.CandidateIndices;
                        break;
                    case BlockInventoryOpenFailed _:
                        return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.InventoryOpenFailed);
                    case BlockDetailLoading _:
                        return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.DetailLoading);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(openContext.Restriction), openContext.Restriction, "未知のブロックスロット制約。PlanShiftIntoBlock に分岐を足すこと");
                }
                return PlanAllocation(from, source, ItemMoveArea.Block, openContext.BlockItemSlots, candidateIndices);
            }

            // 枠数が縮んだ直後の古い制約は、範囲外の候補番号を持つ
            // A stale restriction right after the slot count shrank carries out-of-range candidates
            bool TryFindOutOfRangeCandidate(IReadOnlyList<int> candidates, int slotCount, out int staleCandidate)
            {
                foreach (var candidate in candidates)
                {
                    if (candidate < 0 || slotCount <= candidate)
                    {
                        staleCandidate = candidate;
                        return true;
                    }
                }
                staleCandidate = 0;
                return false;
            }

            // 配分の唯一の入口。未知の ID は最大スタックが引けず DirectMovePlanner が例外になるので断る（D-65）
            // The single allocation entry: refuse an unknown ID, which has no max stack and would make DirectMovePlanner throw (D-65)
            PlayerSlotActionPlan PlanAllocation(ItemMoveSlotRef from, SlotItem source, ItemMoveArea targetArea, IReadOnlyList<SlotItem> targetSlots, IReadOnlyList<int> candidateIndices)
            {
                if (!context.StackLevels.ExistsItem(source.ItemId)) return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.UnknownItem);

                // 宛先が全部埋まって 1 件も配分できないときは、無音にせず満杯の断りにする
                // When the destination is full and nothing can be allocated, refuse as full instead of staying silent
                var moves = DirectMovePlanner.PlanMoveActions(from, source, targetArea, targetSlots, candidateIndices, context.StackLevels);
                if (moves.Count == 0) return new ShiftMoveRefusedPlan(ShiftMoveRefusalReason.DestinationFull);
                return new ItemActionsPlan(moves);
            }

            ItemActionsPlan ActionsOf(PlannedItemAction action)
            {
                return new ItemActionsPlan(new[] { action });
            }

            #endregion
        }

        // 右: 掴み中は 1 個置き / 空手は split（半分の計算はコマンド側）
        // Right: place one while holding / split empty-handed (the command computes the half)
        public static IReadOnlyList<PlannedItemAction> PlanRightClick(ItemMoveSlotRef target, SlotItem slot, int grabCount)
        {
            if (0 < grabCount) return new[] { PlannedItemAction.MoveItem(ItemMoveSlotRef.Grab, target, 1) };
            if (slot.IsEmpty) return Array.Empty<PlannedItemAction>();
            return new[] { PlannedItemAction.Split(target) };
        }

        // ダブル: 集める先はコマンド側が現在の持ち手で決める
        // Double: the command picks the target from the current grab
        public static IReadOnlyList<PlannedItemAction> PlanDoubleClick(ItemMoveSlotRef target)
        {
            return new[] { PlannedItemAction.Collect(target) };
        }
    }
}
