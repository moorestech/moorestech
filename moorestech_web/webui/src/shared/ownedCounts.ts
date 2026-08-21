import type { SlotData } from "@/bridge";

// 所持の唯一の判定。集計と先頭スロット探索で同じ述語を物理的に共有する
// The single owned predicate, physically shared by the tally and the first-slot lookup
export function isOwnedSlot(slot: SlotData): boolean {
  return slot.itemId > 0 && slot.count > 0;
}

// スロット列から itemId 別の所持数を集計する。空スロット・0個は除外（recipe/research 共用）
// Tally owned counts per itemId from slots, skipping empties and zero counts (shared by recipe/research)
export function buildOwnedCounts(slots: SlotData[]): Map<number, number> {
  const owned = new Map<number, number>();
  for (const slot of slots) {
    if (!isOwnedSlot(slot)) continue;
    owned.set(slot.itemId, (owned.get(slot.itemId) ?? 0) + slot.count);
  }
  return owned;
}

// 重複時は先頭スロットのみアンカーを担当
// Duplicate anchors never resolve, so only the first owned slot per item carries it
export function firstSlotIndexByItemId(slots: ReadonlyArray<SlotData>): Map<number, number> {
  const result = new Map<number, number>();
  slots.forEach((slot, index) => {
    if (!isOwnedSlot(slot) || result.has(slot.itemId)) return;
    result.set(slot.itemId, index);
  });
  return result;
}

type ItemRequirement = { itemId: number; count: number };

// 全必要アイテムを所持数が満たすか判定する
// Determine whether owned counts satisfy every required item
export function hasEnoughItems(requirements: readonly ItemRequirement[], owned: Map<number, number>): boolean {
  return requirements.every((requirement) => ownedCountOf(owned, requirement.itemId) >= requirement.count);
}

// 未登録なら0を返す既定込みの所持数取得(呼び出し側での`?? 0`重複記述を防ぐ)
// Owned-count lookup with the missing-entry-means-0 default baked in (avoids repeated `?? 0` at call sites)
export function ownedCountOf(owned: Map<number, number>, itemId: number): number {
  return owned.get(itemId) ?? 0;
}
