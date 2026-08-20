import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { CraftRecipe } from "@/bridge";
import { L } from "@/shared/i18n";
import { setDictionaries } from "@/shared/i18n/i18nStore";

vi.mock("@mantine/core", () => ({
  Box: ({ children, ...props }: { children: unknown }) => createElement("mock-box", props, children as never),
  Button: ({ children, ...props }: { children: unknown }) => createElement("mock-button", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  ProgressArrowGlyph: (props: object) => createElement("mock-progress-arrow-glyph", props),
}));
vi.mock("@/bridge", async (importOriginal) => ({
  ...await importOriginal<typeof import("@/bridge")>(),
  dispatchAction: vi.fn(),
  useItemMaster: () => new Map(),
}));

import CraftRecipeEntry from "./CraftRecipeEntry";

const recipe: CraftRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  resultItemId: 100,
  resultCount: 1,
  craftTime: 2,
  requiredItems: [],
};

// 秒数はRecipeRowが矢印の上へ描く。testIdはレシピ単位のtestId+"-duration"
// RecipeRow renders the duration above the arrow under the per-recipe testId + "-duration"
function durationText(renderer: ReactTestRenderer) {
  return renderer.root.findByProps({ "data-testid": `craft-recipe-box-${recipe.recipeGuid}-duration` }).props.children;
}

function renderEntry() {
  let renderer: ReactTestRenderer;
  act(() => {
    renderer = create(createElement(CraftRecipeEntry, {
      recipe,
      counts: new Map<number, number>(),
      onSelect: vi.fn(),
      testId: `craft-recipe-entry-${recipe.recipeGuid}`,
    }));
  });
  return renderer!;
}

describe("CraftRecipeEntry", () => {
  // 秒数の書式はui.recipe.durationだけが持ち、矢印の上のduration要素へ出る
  // ui.recipe.duration alone owns the seconds format and renders in the duration element above the arrow
  it("矢印の上の秒数をui.recipe.durationの書式で組み、ボタンは秒数を持たない", () => {
    act(() => setDictionaries("japanese", {}, {}, {
      [L.ui.recipe.duration]: "{seconds}秒",
      [L.ui.recipe.craftButtonLabel]: "クラフト",
      [L.ui.recipe.holdToCraft]: "長押しでクラフト",
    }));

    const renderer = renderEntry();

    expect(durationText(renderer)).toBe("2秒");
    expect(renderer.root.findByType("mock-button" as never).props.children).toBe("クラフト");
  });

  it("言語ごとの秒数書式を矢印上の表示へ反映する", () => {
    act(() => setDictionaries("english", {}, {
      [L.ui.recipe.duration]: "{seconds} seconds",
      [L.ui.recipe.craftButtonLabel]: "Craft",
      [L.ui.recipe.holdToCraft]: "Hold to craft",
    }, {}));

    const renderer = renderEntry();

    expect(durationText(renderer)).toBe("2 seconds");
    expect(renderer.root.findByType("mock-button" as never).props.children).toBe("Craft");
  });
});
