// 初期タブがmachine到着後のデータで決まること、別ブロックの再マウントで決め直されることを検証する
// Verifies the initial tab is decided from machine data that has already arrived, and is re-decided when a different block remounts
import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";

const recipeGuid = "84000000-0000-4000-8000-000000000001";
const blockGuid = "85000000-0000-4000-8000-000000000001";
const emptyGuid = "00000000-0000-0000-0000-000000000000";

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopic: () => ({
    recipes: [{
      recipeGuid, blockGuid, blockId: 10, time: 7,
      inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 2, count: 1 }],
      inputFluids: [], outputFluids: [],
    }],
  }),
}));
vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  ModeSwitch: (props: object) => createElement("mock-mode-switch", props),
}));
vi.mock("./machine/MachineInventoryBody", () => ({
  default: (props: object) => createElement("mock-inventory-body", props),
}));
vi.mock("./machine/MachineRecipeSelectionTab", () => ({
  default: (props: object) => createElement("mock-recipe-selection-tab", props),
}));

import MachineSection from "./MachineSection";

function machineData(selectedRecipeGuid: string, currentState: MachineDetailData["currentState"]): MachineDetailData {
  return {
    recipeGuid: emptyGuid,
    selectedRecipeGuid,
    blockGuid,
    recipeTime: 15,
    outputItems: [{ itemId: 2, count: 1 }],
    currentState,
    currentPower: 50,
    requestPower: 100,
    slotLayout: { input: 1, output: 1, module: 0, inputTank: 0 },
  };
}

function blockData(identifier: string, machine: MachineDetailData): BlockInventoryOpen {
  return {
    open: true, source: "block", blockType: "ElectricMachine", identifier, blockGuid,
    itemSlots: [], fluidSlots: [], machine,
  };
}

function renderSection(identifier: string, machine: MachineDetailData) {
  return create(createElement(MachineSection, { data: blockData(identifier, machine), machine }));
}

describe("MachineSection", () => {
  it("未選択の機械はレシピ選択タブ、選択済みはインベントリタブから始まる", () => {
    const unselected = renderSection("block:1", machineData(emptyGuid, "idle"));
    expect(unselected.root.findByType("mock-mode-switch" as never).props.value).toBe("recipes");

    const selected = renderSection("block:2", machineData(recipeGuid, "idle"));
    expect(selected.root.findByType("mock-mode-switch" as never).props.value).toBe("inventory");
  });

  it("別ブロックへ切り替わった再マウントで初期タブを決め直す", () => {
    // BlockInventoryPanelがidentifierでkey付与するため、別ブロックは必ず再マウントされる
    // BlockInventoryPanel keys by identifier, so a different block always remounts this section
    const selected = renderSection("block:1", machineData(recipeGuid, "idle"));
    expect(selected.root.findByType("mock-mode-switch" as never).props.value).toBe("inventory");

    const remounted = renderSection("block:2", machineData(emptyGuid, "idle"));
    expect(remounted.root.findByType("mock-mode-switch" as never).props.value).toBe("recipes");
  });

  it("停止中は充足率テキストを出さず状態ラベルだけを見せる", () => {
    const halted = renderSection("block:1", machineData(recipeGuid, "halted"));
    expect(halted.root.findAllByProps({ "data-testid": "machine-power-rate" })).toHaveLength(0);

    const processing = renderSection("block:2", machineData(recipeGuid, "processing"));
    expect(processing.root.findAllByProps({ "data-testid": "machine-power-rate" }).length).toBeGreaterThan(0);
  });
});
