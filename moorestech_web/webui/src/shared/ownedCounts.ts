import type { SlotData } from "@/bridge";

// スロット列から itemId 別の所持数を集計する。空スロット・0個は除外（recipe/research 共用）
// Tally owned counts per itemId from slots, skipping empties and zero counts (shared by recipe/research)
export function buildOwnedCounts(slots: SlotData[]): Map<number, number> {
  const owned = new Map<number, number>();
  for (const slot of slots) {
    if (slot.itemId <= 0 || slot.count <= 0) continue;
    owned.set(slot.itemId, (owned.get(slot.itemId) ?? 0) + slot.count);
  }
  return owned;
}

type ItemRequirement = { itemId: number; count: number };

// 全必要アイテムを所持数が満たすか判定する
// Determine whether owned counts satisfy every required item
export function hasEnoughItems(requirements: readonly ItemRequirement[], owned: Map<number, number>): boolean {
  return requirements.every((requirement) => (owned.get(requirement.itemId) ?? 0) >= requirement.count);
}
