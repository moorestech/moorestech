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
  // 秒数の書式はui.recipe.durationだけが持つ
  // ui.recipe.duration alone owns the seconds format
  it("クラフトボタンの秒数をui.recipe.durationの書式で組む", () => {
    act(() => setDictionaries("japanese", {}, {}, {
      [L.ui.recipe.duration]: "{seconds}秒",
      [L.ui.recipe.craftButtonLabel]: "クラフト（{duration}）",
      [L.ui.recipe.holdToCraft]: "長押しでクラフト",
    }));

    const renderer = renderEntry();

    expect(renderer.root.findByType("mock-button" as never).props.children).toBe("クラフト（2秒）");
  });

  it("言語ごとの秒数書式をボタンラベルへ反映する", () => {
    act(() => setDictionaries("english", {}, {
      [L.ui.recipe.duration]: "{seconds} seconds",
      [L.ui.recipe.craftButtonLabel]: "Craft ({duration})",
      [L.ui.recipe.holdToCraft]: "Hold to craft",
    }, {}));

    const renderer = renderEntry();

    expect(renderer.root.findByType("mock-button" as never).props.children).toBe("Craft (2 seconds)");
  });
});
