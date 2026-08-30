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
    const tree = create(createElement(MachineRecipeSelectionRow, { row: { recipe, selected: true }, onSelect }));
    const root = tree.root.findByProps({ "data-testid": `machine-recipe-${recipe.recipeGuid}` });

    expect(root.props["data-selected"]).toBe("true");
    expect(tree.root.findByProps({ "data-testid": `machine-recipe-${recipe.recipeGuid}-name` }).props.children).toBe("item-9");
    act(() => root.props.onClick());
    expect(onSelect).toHaveBeenCalledWith(recipe.recipeGuid);
  });
});
