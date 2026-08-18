import type { ResearchNodeData } from "@/bridge";

// 解放物1件分の判別union。種別追加時はUnlockSectionsのSECTIONSテーブルが型で強制される
// Discriminated union for one unlock entry; adding a kind forces UnlockSections' SECTIONS table to cover it
export type UnlockEntry =
  | { kind: "block"; blockId: number; blockGuid: string }
  | { kind: "machineRecipeOutput"; recipeGuid: string; itemIds: number[]; fluids: { fluidId: number; fluidGuid: string; amount: number }[] }
  | { kind: "itemRecipeView"; itemId: number }
  | { kind: "rewardItem"; itemId: number; count: number }
  | { kind: "connectTool"; guid: string }
  | { kind: "trainCar"; guid: string };

// ノードの解放/報酬フィールドを表示用の単一リストへ写す（種別ごとの直読みをここへ集約）
// Map a node's unlock/reward fields into a single display list (centralizes per-kind direct reads)
export function toUnlockEntries(node: ResearchNodeData): UnlockEntry[] {
  const entries: UnlockEntry[] = [];
  for (const b of node.unlockBlocks) entries.push({ kind: "block", blockId: b.blockId, blockGuid: b.blockGuid });
  for (const r of node.unlockMachineRecipes)
    entries.push({ kind: "machineRecipeOutput", recipeGuid: r.recipeGuid, itemIds: r.outputItemIds, fluids: r.outputFluids });
  for (const itemId of node.unlockItemRecipeViewItemIds) entries.push({ kind: "itemRecipeView", itemId });
  for (const reward of node.rewardItems) entries.push({ kind: "rewardItem", itemId: reward.itemId, count: reward.count });
  for (const guid of node.unlockConnectToolGuids) entries.push({ kind: "connectTool", guid });
  for (const guid of node.unlockTrainCarGuids) entries.push({ kind: "trainCar", guid });
  return entries;
}
