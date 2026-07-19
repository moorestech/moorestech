// 機械レシピ選択行の表示省略と左右クリックActionを検証する
// Verifies machine recipe row omission and left/right click actions
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen } from "@/bridge";

const dispatchMock = vi.hoisted(() => vi.fn());
const topicState = vi.hoisted(() => ({ machineRecipes: null as null | { recipes: object[] } }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: dispatchMock,
  useTopic: () => topicState.machineRecipes,
}));
vi.mock("@/shared/i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  SlotGrid: ({ children, ...props }: { children: unknown }) => createElement("mock-slot-grid", props, children as never),
}));

import MachineRecipeSelectionRow from "./MachineRecipeSelectionRow";

const machineData = {
  open: true, source: "block", blockType: "ElectricMachine", identifier: "(0, 0, 0)", blockName: "Machine",
  itemSlots: [], fluidSlots: [],
  machine: {
    recipeGuid: "", selectedRecipeGuid: "recipe-a", blockGuid: "block-a", recipeTime: 1, outputItems: [],
    currentState: "idle", currentPower: 0, requestPower: 0, slotLayout: { input: 0, output: 0, module: 0 },
  },
} as unknown as BlockInventoryOpen;

describe("MachineRecipeSelectionRow", () => {
  beforeEach(() => {
    dispatchMock.mockClear();
    topicState.machineRecipes = null;
  });

  it("machineなし、topic未受信、対象レシピ0件では行を描画しない", () => {
    const withoutMachine = { ...machineData, machine: undefined } as BlockInventoryOpen;
    expect(create(createElement(MachineRecipeSelectionRow, { data: withoutMachine })).toJSON()).toBeNull();
    expect(create(createElement(MachineRecipeSelectionRow, { data: machineData })).toJSON()).toBeNull();

    topicState.machineRecipes = { recipes: [recipe("other", "block-b", 2)] };
    expect(create(createElement(MachineRecipeSelectionRow, { data: machineData })).toJSON()).toBeNull();
  });

  it("代表出力を並べ、左選択と選択中のみ右解除を送る", () => {
    topicState.machineRecipes = { recipes: [
      recipe("recipe-a", "block-a", 2),
      recipe("recipe-b", "block-a", 3),
      recipe("recipe-c", "block-b", 4),
    ] };
    const renderer = create(createElement(MachineRecipeSelectionRow, { data: machineData }));
    const slots = renderer.root.findAllByType("mock-item-slot" as never);

    expect(renderer.root.findByProps({ "data-testid": "machine-recipe-selection" })).toBeTruthy();
    expect(slots).toHaveLength(2);
    expect(slots[0].props).toMatchObject({ itemId: 2, count: 1, selected: true, testId: "machine-recipe-recipe-a" });

    act(() => slots[1].props.onLeftDown());
    act(() => slots[1].props.onRightDown());
    act(() => slots[0].props.onRightDown());

    expect(dispatchMock).toHaveBeenNthCalledWith(1, "machine_recipe.select", { operation: "set", recipeGuid: "recipe-b" });
    expect(dispatchMock).toHaveBeenNthCalledWith(2, "machine_recipe.select", { operation: "clear" });
    expect(dispatchMock).toHaveBeenCalledTimes(2);
  });
});

function recipe(recipeGuid: string, blockGuid: string, itemId: number) {
  return {
    recipeGuid, blockGuid, blockId: 10, blockName: "Machine", time: 1,
    inputItems: [], outputItems: [{ itemId, count: 1 }],
  };
}
