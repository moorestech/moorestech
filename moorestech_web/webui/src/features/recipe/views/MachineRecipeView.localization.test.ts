import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { blockNameKey, L } from "@/shared/i18n";
import { setDictionaries } from "@/shared/i18n/i18nStore";

vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  BlockSlot: (props: object) => createElement("mock-block-slot", props),
}));
vi.mock("./RecipePager", () => ({
  default: (props: object) => createElement("mock-recipe-pager", props),
}));

import MachineRecipeView from "./MachineRecipeView";

const recipe: MachineRecipe = {
  recipeGuid: "recipe-a",
  blockGuid: "ABCDEFAB-CDEF-ABCD-EFAB-CDEFABCDEFAB",
  blockId: 10,
  time: 1,
  inputItems: [],
  outputItems: [],
};

describe("MachineRecipeView localization", () => {
  it("機械名をblockGuidから解決し、fallbackと言語変更をスロットaltと表示へ反映する", () => {
    const key = blockNameKey(recipe.blockGuid);
    act(() => setDictionaries("japanese", {}, {
      [key]: "Fallback Machine",
      [L.ui.common.rightArrow]: "→",
    }, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(MachineRecipeView, {
        recipes: [recipe],
        recipeIndex: 0,
        setRecipeIndex: vi.fn(),
        onSelect: vi.fn(),
      }));
    });

    expect(renderer!.root.findByType("mock-block-slot" as never).props.name).toBe("Fallback Machine");
    expect(renderer!.root.findAllByType("mock-text" as never).some((node) => node.props.children === "Fallback Machine")).toBe(true);

    // 辞書変更はtopicを再配信せずに機械名を更新する
    // Dictionary changes update the machine name without republishing the topic
    act(() => setDictionaries("japanese", {
      [key]: "対象言語の機械",
      [L.ui.common.rightArrow]: "→",
    }, {}, {}));
    expect(renderer!.root.findByType("mock-block-slot" as never).props.name).toBe("対象言語の機械");
    expect(renderer!.root.findAllByType("mock-text" as never).some((node) => node.props.children === "対象言語の機械")).toBe(true);
  });
});
