import { describe, expect, it } from "vitest";
import { validateTopicPayload } from "./validators";
import { Topics } from "../transport/protocol";

const openBase = {
  open: true, source: "block", blockType: "ElectricMachine", identifier: "(0, 0, 0)", blockGuid: "40000000-0000-4000-8000-000000000001",
  itemSlots: [{ itemId: 1, count: 2 }], fluidSlots: [],
};

describe("placement mode schema", () => {
  it("accepts block Guid without a raw display name", () => {
    expect(validateTopicPayload(Topics.placementMode, {
      selectedTargetType: "block",
      selectedBlockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
      height: 2,
      unavailableReason: "",
    })).toBe(true);
  });

  it("accepts a raw display name only for non-block targets", () => {
    expect(validateTopicPayload(Topics.placementMode, {
      selectedTargetType: "raw",
      selectedName: "My Blueprint", height: 2, unavailableReason: "",
    })).toBe(true);
    expect(validateTopicPayload(Topics.placementMode, {
      selectedTargetType: "block",
      selectedBlockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
      selectedName: "Conveyor Belt",
      height: 2,
      unavailableReason: "",
    })).toBe(false);
    expect(validateTopicPayload(Topics.placementMode, {
      selectedTargetType: "raw", selectedName: "Conveyor Belt",
    })).toBe(false);
  });

  it("accepts trainCar Guid without a raw display name", () => {
    expect(validateTopicPayload(Topics.placementMode, {
      selectedTargetType: "trainCar",
      selectedTrainCarGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefad",
      height: 2,
      unavailableReason: "",
    })).toBe(true);
    expect(validateTopicPayload(Topics.placementMode, {
      selectedTargetType: "trainCar",
      selectedTrainCarGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefad",
      selectedName: "Train Car",
      height: 2,
      unavailableReason: "",
    })).toBe(false);
  });

  it("accepts Blueprint Copy without a raw display name", () => {
    expect(validateTopicPayload(Topics.placementMode, {
      selectedTargetType: "blueprintCopy",
      height: 2,
      unavailableReason: "",
    })).toBe(true);
  });
});

describe("common HUD schemas", () => {
  it("accepts crosshair and visibility state", () => {
    expect(validateTopicPayload(Topics.crosshair, { visible: true })).toBe(true);
    expect(validateTopicPayload(Topics.uiVisibility, { visible: false })).toBe(true);
    expect(validateTopicPayload(Topics.crosshair, {})).toBe(false);
  });
});

describe("tooltip schema", () => {
  it("requires a complete cursor-tooltip snapshot", () => {
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, textKey: "ui.tooltip.requiredItems", textParams: ["Iron Pickaxe"], fontSize: 36,
    })).toBe(true);
    expect(validateTopicPayload(Topics.tooltip, {
      visible: true, textKey: "Cannot remove", fontSize: 36,
    })).toBe(false);
  });
});

describe("localization.current schema", () => {
  it("requires a non-empty locale and a non-negative integer dictionary revision", () => {
    expect(validateTopicPayload(Topics.localization, {
      locale: "japanese", revision: 42,
    })).toBe(true);
    expect(validateTopicPayload(Topics.localization, { locale: "japanese" })).toBe(false);
    expect(validateTopicPayload(Topics.localization, { locale: "" })).toBe(false);
    expect(validateTopicPayload(Topics.localization, {
      locale: "japanese", revision: -1,
    })).toBe(false);
    expect(validateTopicPayload(Topics.localization, {
      locale: "japanese", revision: 1.5,
    })).toBe(false);
  });
});

describe("validBlockInventory capability details", () => {
  it("accepts machine + electricNetwork details", () => {
    const d = {
      ...openBase,
      progress: 0.5,
      machine: { recipeGuid: "50000000-0000-4000-8000-000000000001", selectedRecipeGuid: "50000000-0000-4000-8000-000000000002", blockGuid: "40000000-0000-4000-8000-000000000001", recipeTime: 15, outputItems: [{ itemId: 2, count: 3 }], currentState: "processing", currentPower: 10, requestPower: 20, slotLayout: { input: 2, output: 1, module: 1 } },
      electricNetwork: { totalGeneratePower: 100, totalRequiredPower: 50, consumerCount: 3, powerRate: 1 },
    };
    expect(validateTopicPayload(Topics.blockInventory, d)).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, {
      ...d,
      machine: { ...d.machine, selectedRecipeGuid: undefined, blockGuid: undefined },
    })).toBe(false);
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
    expect(validateTopicPayload(Topics.blockInventory, d)).toBe(true);
  });

  it("rejects electricToGear without output mode power", () => {
    expect(validateTopicPayload(Topics.blockInventory, {
      ...openBase,
      electricToGear: {
        selectedIndex: 0,
        fulfillmentRate: 1,
        consumedElectricPower: 10,
        outputModes: [{ rpm: 10, torque: 10 }],
      },
    })).toBe(false);
  });
  it("rejects malformed details", () => {
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, machine: { recipeGuid: 1 } })).toBe(false);
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, gearNetwork: { totalRequiredGearPower: 1, totalGenerateGearPower: 2, stopReason: 3 } })).toBe(false);
    expect(validateTopicPayload(Topics.blockInventory, { ...openBase, filterSplitter: { directionCount: 1, filterSlotCountPerDirection: 1, directions: [{ mode: "whitelist" }] } })).toBe(false);
  });
  it("still accepts details-less open and closed payloads", () => {
    expect(validateTopicPayload(Topics.blockInventory, openBase)).toBe(true);
    expect(validateTopicPayload(Topics.blockInventory, { open: false })).toBe(true);
  });
});

describe("validResearchTree", () => {
  const node = {
    guid: "60000000-0000-4000-8000-000000000001", state: "researchable", iconItemId: 1, position: { x: 100, y: -50 },
    prevGuids: [], consumeItems: [{ itemId: 1, count: 3 }],
    rewardItems: [{ itemId: 2, count: 4 }], unlockItemIds: [],
  };
  it("accepts nodes payload", () => {
    expect(validateTopicPayload(Topics.researchTree, { nodes: [node] })).toBe(true);
    expect(validateTopicPayload(Topics.researchTree, { nodes: [] })).toBe(true);
  });
  it("rejects malformed node", () => {
    expect(validateTopicPayload(Topics.researchTree, { nodes: [{ ...node, position: { x: "0", y: 0 } }] })).toBe(false);
    expect(validateTopicPayload(Topics.researchTree, {})).toBe(false);
  });
});

describe("validCraftRecipes", () => {
  const recipe = {
    recipeGuid: "50000000-0000-4000-8000-000000000001", resultItemId: 2, resultCount: 1, craftTime: 0.5,
    requiredItems: [{ itemId: 1, count: 3 }],
  };

  it("accepts complete recipe elements", () => {
    expect(validateTopicPayload(Topics.craftRecipes, { recipes: [recipe] })).toBe(true);
  });

  it("rejects recipe elements with a missing required field", () => {
    const { craftTime: _, ...missingCraftTime } = recipe;
    expect(validateTopicPayload(Topics.craftRecipes, { recipes: [missingCraftTime] })).toBe(false);
  });

  it("rejects recipe elements with an invalid nested item type", () => {
    const invalid = { ...recipe, requiredItems: [{ itemId: "1", count: 3 }] };
    expect(validateTopicPayload(Topics.craftRecipes, { recipes: [invalid] })).toBe(false);
  });

  it.each([
    ["requiredItems が null", { ...recipe, requiredItems: null }],
    ["素材数が0", { ...recipe, requiredItems: [{ itemId: 1, count: 0 }] }],
    ["素材IDが小数", { ...recipe, requiredItems: [{ itemId: 1.5, count: 1 }] }],
    ["完成数が負", { ...recipe, resultCount: -1 }],
    ["craftTime が負", { ...recipe, craftTime: -0.1 }],
  ])("React へ危険値を渡さない（%s）", (_label, invalid) => {
    expect(validateTopicPayload(Topics.craftRecipes, { recipes: [invalid] })).toBe(false);
  });
});

describe("validBuildMenu", () => {
  const categoryGuid = "10000000-0000-4000-8000-000000000001";
  const subCategoryGuid = "20000000-0000-4000-8000-000000000001";
  const blockGuid = "30000000-0000-4000-8000-000000000001";
  const categories = [{ categoryGuid, subCategoryGuids: [subCategoryGuid] }];
  const entry = {
    entryType: "block", entryKey: blockGuid, categoryGuid, subCategoryGuid,
    requiredItems: [{ itemId: 3, count: 5 }], iconUrl: "/api/block-icons/1.png",
  };
  it("accepts icon and text entries", () => {
    const d = {
      categories,
      entries: [entry, { entryType: "blueprint", entryKey: "家", label: "家", categoryGuid, subCategoryGuid, requiredItems: [] }],
    };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(true);
  });
  it("rejects a raw label on block master entries", () => {
    const d = { categories, entries: [{ ...entry, label: "鉄の機械" }] };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(false);
  });
  it("rejects a non-Guid category identity", () => {
    const d = { categories: [{ categoryGuid: "物流", subCategoryGuids: [subCategoryGuid] }], entries: [entry] };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(false);
  });
  it("rejects a non-Guid block entry identity", () => {
    const d = { categories, entries: [{ ...entry, entryKey: "1" }] };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(false);
  });
  it("accepts a trainCar entry carrying only its Guid identity", () => {
    const trainCarEntry = {
      entryType: "trainCar",
      entryKey: "8f9c2a51-0000-4000-8000-000000000001",
      categoryGuid,
      subCategoryGuid,
      requiredItems: [],
    };
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [trainCarEntry] })).toBe(true);
    expect(validateTopicPayload(Topics.buildMenu, {
      categories, entries: [{ ...trainCarEntry, entryKey: "master-name" }],
    })).toBe(false);
    expect(validateTopicPayload(Topics.buildMenu, {
      categories, entries: [{ ...trainCarEntry, label: "表示名" }],
    })).toBe(false);
  });
  it("accepts a connectTool entry carrying only its Guid identity", () => {
    const connectToolEntry = {
      entryType: "connectTool",
      entryKey: "40000000-0000-4000-8000-000000000001",
      categoryGuid,
      subCategoryGuid,
      requiredItems: [],
    };
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [connectToolEntry] })).toBe(true);
  });
  it.each([
    ["非Guid identity", { entryKey: "master-name" }],
    ["ホスト解決label", { label: "表示名" }],
  ])("rejects a connectTool entry with %s", (_label, override) => {
    const connectToolEntry = {
      entryType: "connectTool",
      entryKey: "40000000-0000-4000-8000-000000000001",
      categoryGuid,
      subCategoryGuid,
      requiredItems: [],
      ...override,
    };
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [connectToolEntry] })).toBe(false);
  });
  it("rejects a blueprint entry without its user-authored label", () => {
    const d = {
      categories,
      entries: [{ entryType: "blueprint", entryKey: "家", categoryGuid, subCategoryGuid, requiredItems: [] }],
    };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(false);
  });
  it("rejects a non-empty blueprintCopy entry identity", () => {
    const copyEntry = {
      entryType: "blueprintCopy",
      entryKey: "copy",
      categoryGuid,
      subCategoryGuid,
      requiredItems: [],
    };
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [copyEntry] })).toBe(false);
  });
  it("accepts a blueprintCopy entry without a raw label", () => {
    const copyEntry = {
      entryType: "blueprintCopy",
      entryKey: "",
      categoryGuid,
      subCategoryGuid,
      requiredItems: [],
    };
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [copyEntry] })).toBe(true);
  });
  it("rejects a raw label on blueprintCopy entries", () => {
    const copyEntry = {
      entryType: "blueprintCopy",
      entryKey: "",
      label: "ブループリントコピー",
      categoryGuid,
      subCategoryGuid,
      requiredItems: [],
    };
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [copyEntry] })).toBe(false);
  });
  it("rejects an empty user-authored blueprint identity", () => {
    const blueprintEntry = {
      entryType: "blueprint",
      entryKey: "",
      label: "",
      categoryGuid,
      subCategoryGuid,
      requiredItems: [],
    };
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [blueprintEntry] })).toBe(false);
  });
  it("rejects a non-string entryKey", () => {
    const d = { categories, entries: [{ ...entry, entryKey: 1 }] };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(false);
  });
  it("rejects a missing entries array", () => {
    expect(validateTopicPayload(Topics.buildMenu, { categories })).toBe(false);
  });
  it("rejects an entry with a missing categoryGuid", () => {
    const { categoryGuid: _, ...missingCategory } = entry;
    expect(validateTopicPayload(Topics.buildMenu, { categories, entries: [missingCategory] })).toBe(false);
  });
  it("rejects an entry with a non-array requiredItems", () => {
    const d = { categories, entries: [{ ...entry, requiredItems: "not-an-array" }] };
    expect(validateTopicPayload(Topics.buildMenu, d)).toBe(false);
  });
});

describe("validModal input flag", () => {
  const base = { id: "m1", title: "t", message: "m", buttonText: "OK", variant: "confirm" };
  it("accepts input:true", () => {
    expect(validateTopicPayload(Topics.modal, { modal: { ...base, input: true } })).toBe(true);
  });
  it("rejects a non-bool input", () => {
    expect(validateTopicPayload(Topics.modal, { modal: { ...base, input: "yes" } })).toBe(false);
  });
});
