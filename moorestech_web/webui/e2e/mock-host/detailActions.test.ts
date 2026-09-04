// 機械レシピ選択Actionがモックblock状態へ反映される契約を検証する
// Verifies that machine recipe selection actions update mock block state
import { describe, expect, it } from "vitest";
import type { BlockInventoryWireData } from "../../src/bridge/contract/payloadTypes";
import type { ActionPayloads } from "../../src/bridge/transport/protocol";
import { applyMachineRecipeSelect } from "./detailActions";

const emptyGuid = "00000000-0000-0000-0000-000000000000";
const recipeA = "84000000-0000-4000-8000-000000000001";
const recipeB = "84000000-0000-4000-8000-000000000002";
const blockGuid = "85000000-0000-4000-8000-000000000001";

describe("applyMachineRecipeSelect", () => {
  it("setはrecipeGuidを選択状態へ反映する", () => {
    const block = machineBlock();

    expect(applyMachineRecipeSelect(block, { operation: "set", recipeGuid: recipeB })).toBe(true);
    expect(selectedRecipeGuid(block)).toBe(recipeB);
  });

  it("recipeGuidなしsetとclearは空GUIDへ戻す", () => {
    const block = machineBlock();

    expect(applyMachineRecipeSelect(block, { operation: "set" })).toBe(true);
    expect(selectedRecipeGuid(block)).toBe(emptyGuid);
    expect(applyMachineRecipeSelect(block, { operation: "set", recipeGuid: recipeA })).toBe(true);
    expect(applyMachineRecipeSelect(block, { operation: "clear" })).toBe(true);
    expect(selectedRecipeGuid(block)).toBe(emptyGuid);
  });

  it("対象machineがないblockと未知operationを拒否する", () => {
    expect(applyMachineRecipeSelect({ open: false }, { operation: "clear" })).toBe(false);
    const invalid = { operation: "invalid" } as unknown as ActionPayloads["machine_recipe.select"];
    expect(applyMachineRecipeSelect(machineBlock(), invalid)).toBe(false);
  });
});

function machineBlock(): BlockInventoryWireData {
  return {
    open: true,
    source: "block",
    blockType: "ElectricMachine",
    identifier: "block:3",
    blockGuid,
    itemSlots: [],
    fluidSlots: [],
    machine: {
      recipeGuid: emptyGuid,
      selectedRecipeGuid: recipeA,
      blockGuid,
      recipeTime: 1,
      outputItems: [],
      currentState: "idle",
      currentPower: 0,
      requestPower: 0,
      slotLayout: { input: 0, output: 0, module: 0, inputTank: 0 },
      slotBindings: [],
      tankBindings: [],
    },
  };
}

function selectedRecipeGuid(block: BlockInventoryWireData): string | undefined {
  return block.open && "machine" in block ? block.machine?.selectedRecipeGuid : undefined;
}
