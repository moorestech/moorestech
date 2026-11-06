using System;
using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Item.Interface;
using Core.Master;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// 配分 1 件（宛先の番号と個数）
    /// One allocation: destination index and count
    /// </summary>
    public readonly struct PlannedMove : IEquatable<PlannedMove>
    {
        public readonly int Slot;
        public readonly int Count;

        public PlannedMove(int slot, int count)
        {
            Slot = slot;
            Count = count;
        }

        public bool Equals(PlannedMove other) => Slot == other.Slot && Count == other.Count;
        public override bool Equals(object obj) => obj is PlannedMove other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Slot, Count);
        public override string ToString() => $"{Slot}x{Count}";
    }

    /// <summary>
    /// Shift+クリックの直接移動の配分計画（uGUI PlayerInventoryDirectMover 準拠。Web planDirectMoves の写し）
    /// Direct-move allocation for Shift+click (mirrors uGUI PlayerInventoryDirectMover; port of Web planDirectMoves)
    /// </summary>
    public static class DirectMovePlanner
    {
        public static IReadOnlyList<PlannedMove> Plan(int sourceCount, ItemId itemId, int maxStack, IReadOnlyList<SlotItem> targetSlots)
        {
            // 同種スタック（空きあり）へ順に詰める
            // Fill same-item stacks that have room, in order
            var moves = new List<PlannedMove>();
            var remaining = sourceCount;
            for (var i = 0; i < targetSlots.Count && 0 < remaining; i++)
            {
                var target = targetSlots[i];
                if (target.IsEmpty || target.ItemId != itemId || maxStack <= target.Count) continue;
                var count = Math.Min(remaining, maxStack - target.Count);
                moves.Add(new PlannedMove(i, count));
                remaining -= count;
            }

            // ソースは 1 スタック分なので、残り全量は最初の空スロット 1 つで収まる
            // The source is a single stack, so the first empty slot always fits the remainder
            if (remaining <= 0) return moves;
            // silent-ok: 入り切らない分は計画しないだけで元スロットに残る / プレイヤーは元スロットの残数で画面上すぐ気づく（uGUI と同じ）
            if (TryFindFirstEmpty(targetSlots, out var empty)) moves.Add(new PlannedMove(empty, remaining));
            return moves;
        }

        // 配分→操作列の唯一の変換。候補の番号の順に宛先を並べて配分し、配分の番号を候補の番号へ戻す
        // The single allocation-to-actions conversion: allocate over candidates in order, then map back to their indices
        public static IReadOnlyList<PlannedItemAction> PlanMoveActions(ItemMoveSlotRef from, SlotItem source, ItemMoveArea targetArea, IReadOnlyList<SlotItem> targetSlots, IReadOnlyList<int> candidateIndices, IItemStackLevelLookup stackLevels)
        {
            var candidateSlots = new List<SlotItem>(candidateIndices.Count);
            foreach (var index in candidateIndices) candidateSlots.Add(targetSlots[index]);
            var moves = Plan(source.Count, source.ItemId, stackLevels.GetMaxStack(source.ItemId), candidateSlots);

            var actions = new List<PlannedItemAction>(moves.Count);
            foreach (var move in moves) actions.Add(PlannedItemAction.MoveItem(from, new ItemMoveSlotRef(targetArea, candidateIndices[move.Slot]), move.Count));
            return actions;
        }

        // 制約なしの宛先候補（全スロットの番号）
        // Candidates without a restriction: every slot index
        public static IReadOnlyList<int> AllIndicesOf(IReadOnlyList<SlotItem> slots)
        {
            var all = new int[slots.Count];
            for (var i = 0; i < all.Length; i++) all[i] = i;
            return all;
        }

        private static bool TryFindFirstEmpty(IReadOnlyList<SlotItem> slots, out int index)
        {
            for (index = 0; index < slots.Count; index++)
            {
                if (slots[index].IsEmpty) return true;
            }
            index = 0;
            return false;
        }
    }
}
