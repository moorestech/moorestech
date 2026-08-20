import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";
import { blockNameKey, L } from "@/shared/i18n";
import { setDictionaries } from "@/shared/i18n/i18nStore";

vi.mock("@mantine/core", () => ({
  Box: ({ children, ...props }: { children: unknown }) => createElement("mock-box", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  BlockIcon: (props: object) => createElement("mock-block-icon", props),
  ProgressArrowGlyph: (props: object) => createElement("mock-progress-arrow-glyph", props),
}));

import MachineRecipeEntry from "./MachineRecipeEntry";

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
  blockId: 10,
  time: 1,
  inputItems: [],
  outputItems: [],
};

// 秒数はRecipeRowが矢印の上へ描く。testIdはレシピ単位のtestId+"-duration"
// RecipeRow renders the duration above the arrow under the per-recipe testId + "-duration"
function durationText(renderer: ReactTestRenderer) {
  return renderer.root.findByProps({ "data-testid": `machine-recipe-box-${recipe.recipeGuid}-duration` }).props.children;
}

describe("MachineRecipeEntry localization", () => {
  it("機械名をblockGuidから解決し、fallbackと言語変更をアイコンaltと表示・秒数へ反映する", () => {
    const key = blockNameKey(recipe.blockGuid);
    act(() => setDictionaries("japanese", {}, {
      [key]: "Fallback Machine",
      [L.ui.recipe.duration]: "{seconds}s",
    }, {}));
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(MachineRecipeEntry, {
        recipe,
        onSelect: vi.fn(),
        testId: `machine-recipe-entry-${recipe.recipeGuid}`,
      }));
    });

    expect(renderer!.root.findAllByType("mock-text" as never).some((node) => node.props.children === "Fallback Machine")).toBe(true);
    expect(renderer!.root.findByType("mock-block-icon" as never).props.alt).toBe("Fallback Machine");
    expect(durationText(renderer!)).toBe("1s");

    // topic再配信なしで機械名更新
    // Update the machine name without topic republication
    act(() => setDictionaries("japanese", {
      [key]: "対象言語の機械",
      [L.ui.recipe.duration]: "{seconds}秒",
    }, {}, {}));
    expect(renderer!.root.findAllByType("mock-text" as never).some((node) => node.props.children === "対象言語の機械")).toBe(true);
    expect(renderer!.root.findByType("mock-block-icon" as never).props.alt).toBe("対象言語の機械");
    expect(durationText(renderer!)).toBe("1秒");
  });
});
