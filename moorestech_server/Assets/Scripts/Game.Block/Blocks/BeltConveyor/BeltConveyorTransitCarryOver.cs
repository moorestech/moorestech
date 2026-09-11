using System;
using System.Collections.Generic;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    /// 張替え時にベルト搬送品を退避し、新ベルトへ進行率維持で復元する。
    /// どのスロットへ何tickで置くかの判断はこのクラスが持ち、ベルト側は置く操作だけを受ける。
    ///
    /// Collects belt transit items for replace placement and restores them into the new belt keeping progress.
    /// This class owns the decision of which slot and how many ticks; the belt only receives the placement operation.
    /// </summary>
    public static class BeltConveyorTransitCarryOver
    {
        public static List<BeltTransitItem> Collect(IItemCollectableBeltConveyor belt)
        {
            var result = new List<BeltTransitItem>();
            foreach (var item in belt.BeltConveyorItems)
            {
                if (item == null) continue;
                result.Add(new BeltTransitItem(item.ItemId, item.ItemInstanceId, ResolveRemainingRate(item)));
            }
            return result;

            #region Internal

            double ResolveRemainingRate(IOnBeltConveyorItem item)
            {
                // 停止中(総tickが0またはuint.MaxValue)は進行率そのものが存在しないため入口として扱う
                // A stopped belt (total ticks 0 or uint.MaxValue) has no progress to preserve, so the item is treated as being at the entry
                if (item.TotalTicks == 0 || item.TotalTicks == uint.MaxValue) return 1.0;
                return item.RemainingTicks / (double)item.TotalTicks;
            }

            #endregion
        }

        // 復元できなかった分を返す。呼び出し側がプレイヤーへ返す
        // Returns the items that did not fit; the caller hands them to the player
        public static List<BeltTransitItem> Restore(VanillaBeltConveyorComponent belt, IReadOnlyList<BeltTransitItem> items)
        {
            var overflow = new List<BeltTransitItem>();
            foreach (var item in items)
            {
                if (TryRestoreOne(belt, item)) continue;
                overflow.Add(item);
            }
            return overflow;
        }

        private static bool TryRestoreOne(VanillaBeltConveyorComponent belt, BeltTransitItem transitItem)
        {
            var totalTicks = belt.TicksOfItemEnterToExit;
            var slotCount = belt.GetSlotSize();

            var remainingTicks = (uint)Math.Min(totalTicks, Math.Ceiling(transitItem.RemainingRate * totalTicks));
            var slot = FindEmptySlotTowardEntry(ResolveSlot(remainingTicks, slotCount, totalTicks));
            if (slot < 0) return false;

            belt.PlaceRestoredItem(slot, transitItem.ItemId, transitItem.ItemInstanceId, remainingTicks);
            return true;

            #region Internal

            int ResolveSlot(uint ticks, int slots, uint total)
            {
                // 停止中(総tickが0またはuint.MaxValue)の復元先では進行率が意味を持たないため入口スロットへ置く
                // On a stopped target (total ticks 0 or uint.MaxValue) the progress rate is meaningless, so place at the entry slot
                if (total == 0 || total == uint.MaxValue) return slots - 1;

                // Updateのスロット滞在条件「i*tps < RemainingTicks <= (i+1)*tps」を逆に解く
                // Invert Update's dwell rule "i*tps < RemainingTicks <= (i+1)*tps" to get the slot
                var ticksPerSlot = total / (uint)slots;

                // 総tickがスロット数未満でスロット当たりのtickが刻めない場合も入口スロットへ置く
                // Also place at the entry slot when total ticks are fewer than the slots and cannot be subdivided
                if (ticksPerSlot == 0) return slots - 1;

                var resolvedSlot = (int)(((long)ticks + ticksPerSlot - 1) / ticksPerSlot) - 1;
                return Math.Clamp(resolvedSlot, 0, slots - 1);
            }

            int FindEmptySlotTowardEntry(int idealSlot)
            {
                // 出口側(小さいindex)へ落とすとRemainingTicksの単調性が壊れ後続が恒久ブロックされるため、入口側だけを探す
                // Searching toward the exit would break the RemainingTicks ordering and permanently block followers, so only the entry side is searched
                var slots = belt.BeltConveyorItems;
                for (var i = idealSlot; i < slots.Count; i++)
                {
                    if (slots[i] == null) return i;
                }
                return -1;
            }

            #endregion
        }
    }
}
