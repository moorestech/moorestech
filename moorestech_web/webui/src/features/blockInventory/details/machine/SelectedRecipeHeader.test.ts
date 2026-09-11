import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
vi.mock("@mantine/core", () => ({
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  HoverTooltip: ({ children }: { children: unknown }) => createElement("mock-hover-tooltip", null, children as never),
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  FluidAmountSlot: (props: object) => createElement("mock-fluid-amount-slot", props),
}));

import SelectedRecipeHeader from "./SelectedRecipeHeader";

const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "85000000-0000-4000-8000-000000000001",
  blockId: 10, time: 7,
  inputItems: [], outputItems: [],
  inputFluids: [], outputFluids: [{ fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 }],
};

describe("SelectedRecipeHeader", () => {
  // 液体代表も同枠、ヘッダはバッジ無し
  // A fluid representative also shares the frame; the header has no badge
  it("代表が液体のときはFluidAmountSlotをバッジ非表示で描く", () => {
    const subject = { kind: "fluid" as const, fluidGuid: "87000000-0000-4000-8000-000000000001", amount: 100 };
    const tree = create(createElement(SelectedRecipeHeader, { recipe, subject, onChangeRecipe: vi.fn() }));

    const slot = tree.root.findByType("mock-fluid-amount-slot" as never);
    expect(slot.props.fluidGuid).toBe("87000000-0000-4000-8000-000000000001");
    expect(slot.props.badge).toEqual({ kind: "hidden" });
    expect(slot.props.testId).toBe("machine-selected-recipe-fluid");
    expect(tree.root.findAllByType("mock-item-slot" as never)).toHaveLength(0);
  });

  it("代表がアイテムのときはItemSlotを描く", () => {
    const subject = { kind: "item" as const, itemId: 9, count: 1 };
    const tree = create(createElement(SelectedRecipeHeader, { recipe, subject, onChangeRecipe: vi.fn() }));

    expect(tree.root.findByType("mock-item-slot" as never).props.itemId).toBe(9);
    expect(tree.root.findAllByType("mock-fluid-amount-slot" as never)).toHaveLength(0);
  });
});
