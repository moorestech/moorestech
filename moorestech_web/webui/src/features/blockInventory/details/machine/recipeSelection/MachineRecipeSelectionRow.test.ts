import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
vi.mock("@mantine/core", () => ({
  Box: ({ children, ...props }: { children: unknown }) => createElement("mock-box", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  FluidIcon: (props: object) => createElement("mock-fluid-icon", props),
  ProgressArrowGlyph: (props: object) => createElement("mock-arrow", props),
}));

import MachineRecipeSelectionRow from "./MachineRecipeSelectionRow";

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  blockId: 10, time: 7,
  inputItems: [{ itemId: 1, count: 2 }], outputItems: [{ itemId: 9, count: 1 }],
  inputFluids: [], outputFluids: [],
};

describe("MachineRecipeSelectionRow", () => {
  it("レシピ名を出力アイテム名で出し、行クリックで選択通知する", () => {
    const onSelect = vi.fn();
    const row = { recipe, subject: { kind: "item" as const, itemId: 9, count: 1 }, selected: true };
    const tree = create(createElement(MachineRecipeSelectionRow, { row, onSelect }));
    const root = tree.root.findByProps({ "data-testid": `machine-recipe-${recipe.recipeGuid}` });

    expect(root.props["data-selected"]).toBe("true");
    expect(tree.root.findByProps({ "data-testid": `machine-recipe-${recipe.recipeGuid}-name` }).props.children).toBe("item-9");
    act(() => root.props.onClick());
    expect(onSelect).toHaveBeenCalledWith(recipe.recipeGuid);
  });

  // D2回帰: 液体のみ出力のレシピ（ボイラー等）も液体名を代表として描画できる
  // D2 regression: fluid-only-output recipes (e.g. boilers) also render with the fluid name as the representative
  it("代表が液体のときは液体名で行名を出す", () => {
    const fluidOnlyRecipe: MachineRecipe = { ...recipe, outputItems: [], outputFluids: [{ fluidId: 9, fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 }] };
    const row = { recipe: fluidOnlyRecipe, subject: { kind: "fluid" as const, fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 }, selected: false };
    const tree = create(createElement(MachineRecipeSelectionRow, { row, onSelect: vi.fn() }));

    expect(tree.root.findByProps({ "data-testid": `machine-recipe-${fluidOnlyRecipe.recipeGuid}-name` }).props.children)
      .toBe("fluid.87000000-0000-4000-8000-000000000001.name");
  });
});
