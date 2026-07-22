// レシピ選択タブのクリックAction・選択後のタブ遷移通知・ホバー優先の詳細プレビューを検証する
// Verifies recipe tab click actions, the post-select tab jump callback, and the hover-first detail preview
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
  FadeRule: (props: object) => createElement("mock-fade-rule", props),
}));

import MachineRecipeSelectionTab from "./MachineRecipeSelectionTab";

function recipe(recipeGuid: string, itemId: number, time: number): MachineRecipe {
  return {
    recipeGuid, blockGuid: "block-a", blockId: 10, blockName: "Machine", time,
    inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId, count: 1 }],
  };
}

describe("MachineRecipeSelectionTab", () => {
  beforeEach(() => {
    dispatchMock.mockClear();
  });

  it("代表出力を並べ、左選択でActionとタブ遷移通知、選択中のみ右解除を送る", () => {
    const recipes = [recipe("recipe-a", 2, 7), recipe("recipe-b", 3, 9)];
    const rows = buildMachineRecipeSelectionRows(recipes, "block-a", "recipe-a");
    const onSelected = vi.fn();
    const renderer = create(createElement(MachineRecipeSelectionTab, { rows, recipes, onSelected }));
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
    expect(onSelected).toHaveBeenCalledTimes(1);
  });

  it("詳細プレビューはホバー優先・選択中フォールバック・どちらも無ければ案内文", () => {
    const recipes = [recipe("recipe-a", 2, 7), recipe("recipe-b", 3, 9)];
    const rows = buildMachineRecipeSelectionRows(recipes, "block-a", "recipe-a");
    const renderer = create(createElement(MachineRecipeSelectionTab, { rows, recipes, onSelected: vi.fn() }));

    // 選択中レシピが既定の詳細として表示される
    // The selected recipe shows as the default detail
    const timeOf = () => renderer.root.findByProps({ "data-testid": "machine-recipe-detail-time" }).props.children;
    expect(renderer.root.findByProps({ "data-testid": "machine-recipe-detail" })).toBeTruthy();
    expect(timeOf()).toBe("{time}秒");

    // ホバー中は選択より優先し、ホバー解除で選択中へ戻る
    // Hover overrides the selection and leaving hover falls back to it
    const slots = renderer.root.findAll((node) => node.type === ("mock-item-slot" as never) && node.props.testId !== undefined);
    const detailSlotIds = () => renderer.root
      .findByProps({ "data-testid": "machine-recipe-detail" })
      .findAll((node) => node.type === ("mock-item-slot" as never))
      .map((node) => node.props.itemId);
    expect(detailSlotIds()).toEqual([1, 2]);
    act(() => slots[1].props.onHoverChange(true));
    expect(detailSlotIds()).toEqual([1, 3]);
    act(() => slots[1].props.onHoverChange(false));
    expect(detailSlotIds()).toEqual([1, 2]);

    // 未選択・非ホバーでは詳細を出さず案内文を表示する
    // With no selection and no hover, the guidance text replaces the detail
    const unselectedRows = buildMachineRecipeSelectionRows(recipes, "block-a", null);
    const unselected = create(createElement(MachineRecipeSelectionTab, { rows: unselectedRows, recipes, onSelected: vi.fn() }));
    expect(unselected.root.findAllByProps({ "data-testid": "machine-recipe-detail" })).toHaveLength(0);
    expect(unselected.root.findByProps({ "data-testid": "machine-recipe-detail-empty" })).toBeTruthy();
  });
});
