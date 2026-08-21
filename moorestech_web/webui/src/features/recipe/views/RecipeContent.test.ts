import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { CraftRecipesData, MachineRecipesData, PlayerInventoryData } from "@/bridge";
import { TutorialAnchorIds } from "@/shared/tutorialAnchor";

vi.mock("@mantine/core", () => ({
  ScrollArea: { Autosize: ({ children, ...props }: { children: unknown }) => createElement("mock-scroll", props, children as never) },
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("./ItemHeader", () => ({ default: (props: object) => createElement("mock-item-header", props) }));
vi.mock("./CraftRecipeEntry", () => ({ default: (props: object) => createElement("mock-craft-entry", props) }));
vi.mock("./MachineRecipeEntry", () => ({ default: (props: object) => createElement("mock-machine-entry", props) }));

import RecipeContent from "./RecipeContent";

const CRAFT_GUID_A = "84000000-0000-4000-8000-00000000000a";
const CRAFT_GUID_B = "84000000-0000-4000-8000-00000000000b";
const MACHINE_GUID = "84000000-0000-4000-8000-00000000000c";
const RESULT_ITEM_ID = 100;

const recipes: CraftRecipesData = {
  recipes: [
    { recipeGuid: CRAFT_GUID_A, resultItemId: RESULT_ITEM_ID, resultCount: 1, craftTime: 2, requiredItems: [] },
    { recipeGuid: CRAFT_GUID_B, resultItemId: RESULT_ITEM_ID, resultCount: 1, craftTime: 3, requiredItems: [] },
  ],
};
const machineRecipes: MachineRecipesData = {
  recipes: [{
    recipeGuid: MACHINE_GUID,
    blockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
    blockId: 10,
    time: 1,
    inputItems: [],
    outputItems: [{ itemId: RESULT_ITEM_ID, count: 1 }],
  }],
};
const inventory = { mainSlots: [] } as unknown as PlayerInventoryData;

function renderContent() {
  let renderer: ReactTestRenderer;
  act(() => {
    renderer = create(createElement(RecipeContent, { itemId: RESULT_ITEM_ID, recipes, machineRecipes, inventory, onSelect: vi.fn() }));
  });
  return renderer!;
}

describe("RecipeContent", () => {
  // タブ・ページャを介さずクラフト優先の1本のリストへ並べる
  // Every recipe lands in one craft-first list, with no tab or pager in between
  it("全レシピをクラフト優先の単一リストへ並べる", () => {
    const renderer = renderContent();

    const testIds = renderer.root.findAllByType("mock-craft-entry" as never)
      .concat(renderer.root.findAllByType("mock-machine-entry" as never))
      .map((node) => node.props.testId);
    expect(testIds).toEqual([
      `craft-recipe-entry-${CRAFT_GUID_A}`,
      `craft-recipe-entry-${CRAFT_GUID_B}`,
      `machine-recipe-entry-${MACHINE_GUID}`,
    ]);
    // リストは1本だけ（タブ・ページャで分かれない）
    // Exactly one list; no tab or pager splits it
    expect(renderer.root.findAllByType("mock-stack" as never)
      .filter((node) => node.props["data-testid"] === "recipe-entry-list").length).toBe(1);
  });

  it("チュートリアルアンカーを先頭のクラフトエントリ1件だけへ付ける", () => {
    const renderer = renderContent();

    const anchored = renderer.root.findAllByType("mock-craft-entry" as never)
      .filter((node) => node.props.tutorialAnchorProps !== undefined);
    expect(anchored.map((node) => node.props.testId)).toEqual([`craft-recipe-entry-${CRAFT_GUID_A}`]);
    expect(anchored[0].props.tutorialAnchorProps).toEqual({ "data-tutorial-anchor": TutorialAnchorIds.recipeCraftButton });
  });
});
