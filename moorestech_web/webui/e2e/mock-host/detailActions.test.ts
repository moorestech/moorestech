// 機械レシピ選択Actionがモックblock状態へ反映される契約を検証する
// Verifies that machine recipe selection actions update mock block state
import { describe, expect, it } from "vitest";
import type { BlockInventoryData } from "../../src/bridge/contract/payloadTypes";
import type { ActionPayloads } from "../../src/bridge/transport/protocol";
import { applyMachineRecipeSelect } from "./detailActions";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

describe("applyMachineRecipeSelect", () => {
  it("setはrecipeGuidを選択状態へ反映する", () => {
    const block = machineBlock();

    expect(applyMachineRecipeSelect(block, { operation: "set", recipeGuid: "recipe-b" })).toBe(true);
    expect(selectedRecipeGuid(block)).toBe("recipe-b");
  });

  it("recipeGuidなしsetとclearは空GUIDへ戻す", () => {
    const block = machineBlock();

    expect(applyMachineRecipeSelect(block, { operation: "set" })).toBe(true);
    expect(selectedRecipeGuid(block)).toBe(emptyGuid);
    expect(applyMachineRecipeSelect(block, { operation: "set", recipeGuid: "recipe-a" })).toBe(true);
    expect(applyMachineRecipeSelect(block, { operation: "clear" })).toBe(true);
    expect(selectedRecipeGuid(block)).toBe(emptyGuid);
  });

  it("対象machineがないblockと未知operationを拒否する", () => {
    expect(applyMachineRecipeSelect({ open: false }, { operation: "clear" })).toBe(false);
    const invalid = { operation: "invalid" } as unknown as ActionPayloads["machine_recipe.select"];
    expect(applyMachineRecipeSelect(machineBlock(), invalid)).toBe(false);
  });
});

function machineBlock(): BlockInventoryData {
  return {
    open: true,
    source: "block",
    blockType: "ElectricMachine",
    identifier: "block:3",
    blockName: "電気機械",
    itemSlots: [],
    fluidSlots: [],
    machine: {
      recipeGuid: emptyGuid,
      selectedRecipeGuid: "recipe-a",
      blockGuid: "11111111-1111-1111-1111-111111111111",
      recipeTime: 1,
      outputItems: [],
      currentState: "idle",
      currentPower: 0,
      requestPower: 0,
      slotLayout: { input: 0, output: 0, module: 0 },
    },
  };
}

function selectedRecipeGuid(block: BlockInventoryData): string | undefined {
  return block.open && "machine" in block ? block.machine?.selectedRecipeGuid : undefined;
}
