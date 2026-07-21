// レシピ選択タブの左右クリックActionと選択中レシピ詳細を検証する
// Verifies recipe tab left/right click actions and the selected recipe detail
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { buildMachineRecipeSelectionRows } from "./machineRecipeSelectionLogic";

const dispatchMock = vi.hoisted(() => vi.fn());

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: dispatchMock,
}));
vi.mock("@/shared/i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  SlotGrid: ({ children, ...props }: { children: unknown }) => createElement("mock-slot-grid", props, children as never),
}));

import MachineRecipeSelectionTab from "./MachineRecipeSelectionTab";

function recipe(recipeGuid: string, itemId: number): MachineRecipe {
  return {
    recipeGuid, blockGuid: "block-a", blockId: 10, blockName: "Machine", time: 7,
    inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId, count: 1 }],
  };
}

describe("MachineRecipeSelectionTab", () => {
  beforeEach(() => {
    dispatchMock.mockClear();
  });

  it("代表出力を並べ、左選択と選択中のみ右解除を送る", () => {
    const recipes = [recipe("recipe-a", 2), recipe("recipe-b", 3)];
    const rows = buildMachineRecipeSelectionRows(recipes, "block-a", "recipe-a");
    const renderer = create(createElement(MachineRecipeSelectionTab, { rows, recipes }));
    const slots = renderer.root.findAll((node) => node.type === ("mock-item-slot" as never) && node.props.testId !== undefined);

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

  it("選択中レシピの材料と所要時間を詳細へ表示し、未選択では詳細を出さない", () => {
    const recipes = [recipe("recipe-a", 2)];
    const selectedRows = buildMachineRecipeSelectionRows(recipes, "block-a", "recipe-a");
    const selected = create(createElement(MachineRecipeSelectionTab, { rows: selectedRows, recipes }));
    expect(selected.root.findByProps({ "data-testid": "machine-recipe-detail" })).toBeTruthy();
    expect(selected.root.findByProps({ "data-testid": "machine-recipe-detail-time" })).toBeTruthy();
    // 詳細側は材料1+出力1の非操作スロット（testId無し）が並ぶ
    // The detail renders one input and one output as non-interactive slots (no testId)
    const detailSlots = selected.root.findAll((node) => node.type === ("mock-item-slot" as never) && node.props.testId === undefined);
    expect(detailSlots).toHaveLength(2);

    const unselectedRows = buildMachineRecipeSelectionRows(recipes, "block-a", null);
    const unselected = create(createElement(MachineRecipeSelectionTab, { rows: unselectedRows, recipes }));
    expect(unselected.root.findAllByProps({ "data-testid": "machine-recipe-detail" })).toHaveLength(0);
  });
});
