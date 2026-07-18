import type { BlockInventoryData, ResearchTreeData } from "../../src/bridge/contract/payloadTypes";
import type { ActionPayloads } from "../../src/bridge/transport/protocol";

// mock 用の固定 grab アイテムID。clear:false 時に C# 側が持ち手アイテムを設定するのを代替する
// Fixed mock grab item id; stands in for the C# side assigning the currently grabbed item on clear:false
const MOCK_GRAB_ITEM_ID = 999;

// filter_splitter.set_mode: 対象方向の mode を書換える。適用できたら true
// filter_splitter.set_mode: rewrite the target direction's mode; true when applied
export function applyFilterMode(block: BlockInventoryData, p: ActionPayloads["filter_splitter.set_mode"]): boolean {
  if (!block.open || !block.filterSplitter) return false;
  const dir = block.filterSplitter.directions[p.directionIndex];
  if (!dir) return false;
  dir.mode = p.mode;
  return true;
}

// filter_splitter.set_filter_item: filterItemIds[slotIndex] を clear なら0、それ以外は固定grabIDへ書換える
// filter_splitter.set_filter_item: set filterItemIds[slotIndex] to 0 when clear, otherwise the fixed grab id
export function applyFilterItem(block: BlockInventoryData, p: ActionPayloads["filter_splitter.set_filter_item"]): boolean {
  if (!block.open || !block.filterSplitter) return false;
  const dir = block.filterSplitter.directions[p.directionIndex];
  if (!dir || p.slotIndex < 0 || p.slotIndex >= dir.filterItemIds.length) return false;
  dir.filterItemIds[p.slotIndex] = p.clear ? 0 : MOCK_GRAB_ITEM_ID;
  return true;
}

// 有効な出力モードだけ反映する
// electric_to_gear.set_output_mode: apply only a valid index to the StateDetail equivalent
export function applyElectricToGearMode(block: BlockInventoryData, p: ActionPayloads["electric_to_gear.set_output_mode"]): boolean {
  if (!block.open || !block.electricToGear) return false;
  if (p.modeIndex < 0 || p.modeIndex >= block.electricToGear.outputModes.length) return false;
  block.electricToGear.selectedIndex = p.modeIndex;
  return true;
}

// PF capabilityへ目標モードを反映する
// train_platform.set_transfer_mode: apply the target mode only to a platform carrying the capability
export function applyTrainPlatformMode(block: BlockInventoryData, p: ActionPayloads["train_platform.set_transfer_mode"]): boolean {
  if (!block.open || !block.trainPlatform) return false;
  block.trainPlatform.mode = p.mode;
  return true;
}

// research.complete: 該当 guid ノードを completed へ書換える。適用できたら true
// research.complete: rewrite the matching guid node to completed; true when applied
export function applyResearchComplete(tree: ResearchTreeData, p: ActionPayloads["research.complete"]): boolean {
  const node = tree.nodes.find((n) => n.guid === p.researchGuid);
  if (!node) return false;
  node.state = "completed";
  return true;
}
