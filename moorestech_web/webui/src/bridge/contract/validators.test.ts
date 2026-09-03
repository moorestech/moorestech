import { describe, expect, it } from "vitest";
import { parseTopicPayload } from "./validators";
import { Topics } from "../transport/protocol";

const openBase = {
  open: true, source: "block", blockType: "ElectricMachine", identifier: "(0, 0, 0)", blockGuid: "40000000-0000-4000-8000-000000000001",
  itemSlots: [{ itemId: 1, count: 2 }], fluidSlots: [],
};

describe("placement mode schema", () => {
  it("accepts master identities without raw labels", () => {
    expect(parseTopicPayload(Topics.placementMode, {
      selectedTargetType: "block", selectedBlockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
      height: 2, unavailableReason: "", wheelOwnedByTool: false,
    }).valid).toBe(true);
    expect(parseTopicPayload(Topics.placementMode, {
      selectedTargetType: "trainCar", selectedTrainCarGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefad",
      height: 2, unavailableReason: "", wheelOwnedByTool: false,
    }).valid).toBe(true);
    expect(parseTopicPayload(Topics.placementMode, {
      selectedTargetType: "blueprintCopy", height: 2, unavailableReason: "", wheelOwnedByTool: false,
    }).valid).toBe(true);
  });
  it("accepts raw labels only for user-authored targets", () => {
    expect(parseTopicPayload(Topics.placementMode, {
      selectedTargetType: "raw", selectedName: "My Blueprint", height: 2, unavailableReason: "", wheelOwnedByTool: false,
    }).valid).toBe(true);
    expect(parseTopicPayload(Topics.placementMode, {
      selectedTargetType: "block", selectedBlockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
      selectedName: "Conveyor Belt", height: 2, unavailableReason: "", wheelOwnedByTool: false,
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.placementMode, {
      selectedTargetType: "raw", selectedName: "Conveyor Belt",
    }).valid).toBe(false);
  });
});

describe("common HUD schemas", () => {
  it("accepts crosshair and visibility state", () => {
    expect(parseTopicPayload(Topics.crosshair, { visible: true }).valid).toBe(true);
    expect(parseTopicPayload(Topics.uiVisibility, { visible: false }).valid).toBe(true);
    expect(parseTopicPayload(Topics.crosshair, {}).valid).toBe(false);
  });
});

describe("tooltip schema", () => {
  it("requires a complete cursor-tooltip snapshot with lines", () => {
    expect(parseTopicPayload(Topics.tooltip, {
      visible: true, lines: [{ textKey: "ui.tooltip.requiredItems", textParams: ["Iron Pickaxe"] }],
    }).valid).toBe(true);
    expect(parseTopicPayload(Topics.tooltip, { visible: false, lines: [] }).valid).toBe(true);
    expect(parseTopicPayload(Topics.tooltip, {
      visible: true, textKey: "ui.tooltip.requiredItems", textParams: [],
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.tooltip, {
      visible: true, lines: [{ textKey: "Cannot remove" }],
    }).valid).toBe(false);
  });
  // 表示状態はホスト側で行から導出されるため、行と食い違うスナップショットは境界で弾く
  // Visibility is derived from the lines on the host, so a snapshot disagreeing with them is rejected at the boundary
  it("rejects a visibility flag that disagrees with the lines", () => {
    expect(parseTopicPayload(Topics.tooltip, { visible: true, lines: [] }).valid).toBe(false);
    expect(parseTopicPayload(Topics.tooltip, {
      visible: false, lines: [{ textKey: "ui.tooltip.requiredItems", textParams: [] }],
    }).valid).toBe(false);
  });
  it("rejects sizes smuggled in alongside the lines", () => {
    expect(parseTopicPayload(Topics.tooltip, {
      visible: true, lines: [{ textKey: "ui.tooltip.requiredItems", textParams: [] }], width: 240,
    }).valid).toBe(false);
  });
});

describe("localization.current schema", () => {
  it("requires locale and dictionary revision", () => {
    expect(parseTopicPayload(Topics.localization, { locale: "japanese", revision: 42 }).valid).toBe(true);
    expect(parseTopicPayload(Topics.localization, { locale: "japanese" }).valid).toBe(false);
    expect(parseTopicPayload(Topics.localization, { locale: "" }).valid).toBe(false);
    expect(parseTopicPayload(Topics.localization, { locale: "japanese", revision: -1 }).valid).toBe(false);
    expect(parseTopicPayload(Topics.localization, { locale: "japanese", revision: 1.5 }).valid).toBe(false);
  });
});

describe("validBlockInventory capability details", () => {
  it("accepts machine + electricNetwork details", () => {
    const d = {
      ...openBase,
      progress: 0.5,
      machine: { recipeGuid: "50000000-0000-4000-8000-000000000001", selectedRecipeGuid: "50000000-0000-4000-8000-000000000002", blockGuid: "40000000-0000-4000-8000-000000000001", recipeTime: 15, outputItems: [{ itemId: 2, count: 3 }], currentState: "processing", currentPower: 10, requestPower: 20, slotLayout: { input: 2, output: 1, module: 1, inputTank: 0 }, slotBindings: [{ slot: 0, itemId: 1, count: 2 }], tankBindings: [] },
      electricNetwork: { totalGeneratePower: 100, totalRequiredPower: 50, consumerCount: 3, powerRate: 1 },
    };
    expect(parseTopicPayload(Topics.blockInventory, d).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, {
      ...d,
      machine: { ...d.machine, selectedRecipeGuid: undefined, blockGuid: undefined },
    }).valid).toBe(false);
  });
  it("accepts gear + gearNetwork + generator + miner + filterSplitter + electricToGear details", () => {
    const d = {
      ...openBase,
      generator: { remainingFuelTime: 3, currentFuelTime: 10, operatingRate: 0.5 },
      miner: { currentPower: 1, requestPower: 2, miningItems: [{ itemId: 5, itemsPerMinute: 12 }] },
      gear: { isClockwise: true, currentRpm: 10, currentTorque: 3, baseRpm: 20, baseTorque: 5 },
      gearNetwork: { totalRequiredGearPower: 5, totalGenerateGearPower: 10, stopReason: "none" },
      filterSplitter: { directionCount: 2, filterSlotCountPerDirection: 3, directions: [{ mode: "whitelist", filterItemIds: [1, 0, 0] }, { mode: "default", filterItemIds: [0, 0, 0] }] },
      electricToGear: {
        selectedIndex: 1,
        fulfillmentRate: 0.75,
        consumedElectricPower: 10,
        outputModes: [{ rpm: 10, torque: 10, requiredPower: 10 }, { rpm: 20, torque: 20, requiredPower: 10 }],
      },
    };
    expect(parseTopicPayload(Topics.blockInventory, d).valid).toBe(true);
  });

  it("rejects electricToGear without output mode power", () => {
    expect(parseTopicPayload(Topics.blockInventory, {
      ...openBase,
      electricToGear: {
        selectedIndex: 0,
        fulfillmentRate: 1,
        consumedElectricPower: 10,
        outputModes: [{ rpm: 10, torque: 10 }],
      },
    }).valid).toBe(false);
  });
  it("rejects malformed details", () => {
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, machine: { recipeGuid: 1 } }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, gearNetwork: { totalRequiredGearPower: 1, totalGenerateGearPower: 2, stopReason: 3 } }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, { ...openBase, filterSplitter: { directionCount: 1, filterSlotCountPerDirection: 1, directions: [{ mode: "whitelist" }] } }).valid).toBe(false);
  });
  it("still accepts details-less open and closed payloads", () => {
    expect(parseTopicPayload(Topics.blockInventory, openBase).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, { open: false }).valid).toBe(true);
  });
});

describe("validResearchTree", () => {
  const node = {
    guid: "60000000-0000-4000-8000-000000000001", state: "researchable", iconItemId: 1, position: { x: 100, y: -50 },
    prevGuids: [], consumeItems: [{ itemId: 1, count: 3 }],
    rewardItems: [{ itemId: 2, count: 4 }], unlockItemRecipeViewItemIds: [],
    unlockBlocks: [], unlockMachineRecipes: [], unlockConnectToolGuids: [], unlockTrainCarGuids: [],
  };
  it("accepts nodes payload", () => {
    expect(parseTopicPayload(Topics.researchTree, { nodes: [node] }).valid).toBe(true);
    expect(parseTopicPayload(Topics.researchTree, { nodes: [] }).valid).toBe(true);
  });
  it("rejects malformed node", () => {
    expect(parseTopicPayload(Topics.researchTree, { nodes: [{ ...node, position: { x: "0", y: 0 } }] }).valid).toBe(false);
    expect(parseTopicPayload(Topics.researchTree, {}).valid).toBe(false);
  });
});

describe("validMachineRecipes", () => {
  const recipe = {
    recipeGuid: "50000000-0000-4000-8000-000000000001", blockGuid: "40000000-0000-4000-8000-000000000001", blockId: 12, time: 1,
    inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 2, count: 1 }],
    inputFluids: [], outputFluids: [],
  };

  it("accepts BlockId and rejects the removed blockItemId contract", () => {
    expect(parseTopicPayload(Topics.machineRecipes, { recipes: [recipe] }).valid).toBe(true);
    expect(parseTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...recipe, blockId: undefined, blockItemId: 12 }],
    }).valid).toBe(false);
    expect(parseTopicPayload(Topics.machineRecipes, {
      recipes: [{ ...recipe, blockGuid: undefined }],
    }).valid).toBe(false);
  });
});

describe("validCraftRecipes", () => {
  const recipe = {
    recipeGuid: "50000000-0000-4000-8000-000000000001", resultItemId: 2, resultCount: 1, craftTime: 0.5,
    requiredItems: [{ itemId: 1, count: 3 }],
  };

  it("accepts complete recipe elements", () => {
    expect(parseTopicPayload(Topics.craftRecipes, { recipes: [recipe] }).valid).toBe(true);
  });

  it("rejects recipe elements with a missing required field", () => {
    const { craftTime: _, ...missingCraftTime } = recipe;
    expect(parseTopicPayload(Topics.craftRecipes, { recipes: [missingCraftTime] }).valid).toBe(false);
  });

  it("rejects recipe elements with an invalid nested item type", () => {
    const invalid = { ...recipe, requiredItems: [{ itemId: "1", count: 3 }] };
    expect(parseTopicPayload(Topics.craftRecipes, { recipes: [invalid] }).valid).toBe(false);
  });

  it.each([
    ["requiredItems が null", { ...recipe, requiredItems: null }],
    ["素材数が0", { ...recipe, requiredItems: [{ itemId: 1, count: 0 }] }],
    ["素材IDが小数", { ...recipe, requiredItems: [{ itemId: 1.5, count: 1 }] }],
    ["完成数が負", { ...recipe, resultCount: -1 }],
    ["craftTime が負", { ...recipe, craftTime: -0.1 }],
  ])("React へ危険値を渡さない（%s）", (_label, invalid) => {
    expect(parseTopicPayload(Topics.craftRecipes, { recipes: [invalid] }).valid).toBe(false);
  });
});

describe("validModal input flag", () => {
  const base = { id: "m1", title: "t", message: "m", buttonText: "OK", variant: "confirm" };
  it("accepts input:true", () => {
    expect(parseTopicPayload(Topics.modal, { modal: { ...base, input: true } }).valid).toBe(true);
  });
  it("rejects a non-bool input", () => {
    expect(parseTopicPayload(Topics.modal, { modal: { ...base, input: "yes" } }).valid).toBe(false);
  });
});

describe("event_mode.language_gate schema", () => {
  it("boolean の waiting だけを受理する", () => {
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: true }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: false }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, {}).valid).toBe(false);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: "true" }).valid).toBe(false);
  });
});
