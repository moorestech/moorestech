import type { BlockInventoryData } from "@/bridge";
import type { BlockSlotRestriction } from "@/shared/itemMove";
import { boundMachineInputSlotsForItem } from "@/features/blockInventory/details/machine/machineSlotGhosts";
import { splitSlotIndices } from "@/features/blockInventory/details/detailLogic";

type InputSlotBinding = { slot: number; acceptedItemIds: number[] };

// Shift配分の宛先候補を種別ごとに決める
// Settles Shift-allocation candidates per block type; the host-published bindings are the source of truth and no rule is re-derived here
export function resolveBlockSlotRestriction(block: BlockInventoryData | null, itemId: number): BlockSlotRestriction {
  if (!block?.open || block.source !== "block") return { kind: "unrestricted" };

  // 取得失敗は候補探索前に呼出側へ返す
  // Propagate fetch failure before looking up candidate slots
  if (block.error === "openFailed") return { kind: "inventoryOpenFailed" };

  // 入れ口固定の種別は出力枠へ入れない
  // Launch pads and space elevators fix the accepted items per inlet, and their output slots are never insertion targets
  if (block.blockType === "RocketLaunchPad") return boundByInputSlotBindings(block.rocketLaunchPad?.slotBindings, itemId);
  if (block.blockType === "SpaceElevatorBase") return boundByInputSlotBindings(block.spaceElevator?.slotBindings, itemId);
  return resolveMachineSlotRestriction(block, itemId);
}

// 束縛未着は取得待ちとして区別する
// Keep pending bindings distinct from a confirmed refusal
function boundByInputSlotBindings(bindings: InputSlotBinding[] | undefined, itemId: number): BlockSlotRestriction {
  if (!bindings) return { kind: "detailLoading" };
  return { kind: "bound", candidateIndices: bindings.filter((binding) => binding.acceptedItemIds.includes(itemId)).map((binding) => binding.slot) };
}

// 機械の入力帯だけを束縛で絞り、出力帯・モジュール帯は候補に残す（束縛はホスト配信のslotBindingsが正本）。
// 機械以外（チェスト等）は無制限
// Only the machine's input band is narrowed by the binding; the output and module bands stay candidates
// (the host-published slotBindings are the source of truth). Non-machines (chests etc.) stay unrestricted
function resolveMachineSlotRestriction(block: Extract<BlockInventoryData, { source: "block" }>, itemId: number): BlockSlotRestriction {
  if (!block.machine) return { kind: "unrestricted" };
  const machine = block.machine;
  if (machine.slotBindings.length === 0) return { kind: "unrestricted" };

  const { output, module } = splitSlotIndices(machine.slotLayout, block.itemSlots.length);
  const boundInputs = boundMachineInputSlotsForItem(machine, block.itemSlots.length, itemId);
  return { kind: "bound", candidateIndices: [...boundInputs, ...output, ...module] };
}
