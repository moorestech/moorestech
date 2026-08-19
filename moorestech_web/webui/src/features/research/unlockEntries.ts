import type { ResearchNodeData } from "@/bridge";

// 解放union追加はSECTIONS強制
// indexはkey重複判別用(C3)
// A new unlock kind is enforced by the SECTIONS table
// index disambiguates duplicate-valued keys (C3)
export type UnlockEntry =
  | { kind: "block"; index: number; blockId: number; blockGuid: string }
  | { kind: "machineRecipeOutput"; index: number; recipeGuid: string; itemIds: number[]; fluids: { fluidId: number; fluidGuid: string; amount: number }[] }
  | { kind: "itemRecipeView"; index: number; itemId: number }
  | { kind: "rewardItem"; index: number; itemId: number; count: number }
  | { kind: "connectTool"; index: number; guid: string }
  | { kind: "trainCar"; index: number; guid: string };

// 解放/報酬フィールドを表示用リストへ集約
// Maps unlock/reward fields into a single display list
export function toUnlockEntries(node: ResearchNodeData): UnlockEntry[] {
  const entries: UnlockEntry[] = [];
  for (const b of node.unlockBlocks) entries.push({ kind: "block", index: entries.length, blockId: b.blockId, blockGuid: b.blockGuid });
  for (const r of node.unlockMachineRecipes)
    entries.push({ kind: "machineRecipeOutput", index: entries.length, recipeGuid: r.recipeGuid, itemIds: r.outputItemIds, fluids: r.outputFluids });
  for (const itemId of node.unlockItemRecipeViewItemIds) entries.push({ kind: "itemRecipeView", index: entries.length, itemId });
  for (const reward of node.rewardItems) entries.push({ kind: "rewardItem", index: entries.length, itemId: reward.itemId, count: reward.count });
  for (const guid of node.unlockConnectToolGuids) entries.push({ kind: "connectTool", index: entries.length, guid });
  for (const guid of node.unlockTrainCarGuids) entries.push({ kind: "trainCar", index: entries.length, guid });
  return entries;
}
