import type { PlayerInventoryData, CraftRecipe } from "@/bridge/payloadTypes";

// 所持数集計。サーバーの OneClickCraft が main+hotbar のみ見るため grab は除外。
// Owned-count tally; grab is excluded because the server's OneClickCraft only consults main+hotbar.
export function buildOwnedCounts(inventory: PlayerInventoryData): Map<number, number> {
  const counts = new Map<number, number>();
  const add = (id: number, count: number) => {
    if (count > 0) counts.set(id, (counts.get(id) ?? 0) + count);
  };
  inventory.mainSlots.forEach((s) => add(s.itemId, s.count));
  inventory.hotbarSlots.forEach((s) => add(s.itemId, s.count));
  return counts;
}

// 全必要素材を所持数が満たすか。
// Whether owned counts satisfy every required material.
export function craftable(recipe: CraftRecipe, counts: Map<number, number>): boolean {
  return recipe.requiredItems.every((r) => (counts.get(r.itemId) ?? 0) >= r.count);
}

// recipeIndex を [0, length-1] にクランプ。呼び出し側が length>0 を保証する契約。
// Clamp recipeIndex into [0, length-1]; caller must guarantee length>0.
export function clampIndex(index: number, length: number): number {
  return Math.max(0, Math.min(index, length - 1));
}
