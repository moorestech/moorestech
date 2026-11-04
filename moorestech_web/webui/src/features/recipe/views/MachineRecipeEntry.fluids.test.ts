import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineRecipe } from "@/bridge";

vi.mock("@mantine/core", () => ({
  Box: ({ children, ...props }: { children: unknown }) => createElement("mock-box", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));
vi.mock("@/shared/ui", () => ({
  ItemSlot: (props: object) => createElement("mock-item-slot", props),
  FluidAmountSlot: (props: object) => createElement("mock-fluid-amount-slot", props),
  BlockIcon: (props: object) => createElement("mock-block-icon", props),
  ProgressArrowGlyph: (props: object) => createElement("mock-progress-arrow-glyph", props),
}));

import MachineRecipeEntry from "./MachineRecipeEntry";

const WATER = "86000000-0000-4000-8000-000000000001";
const STEAM = "86000000-0000-4000-8000-000000000002";
const recipe: MachineRecipe = {
  recipeGuid: "84000000-0000-4000-8000-000000000001",
  blockGuid: "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
  blockId: 10,
  time: 1,
  inputItems: [{ itemId: 3, count: 2 }],
  outputItems: [{ itemId: 4, count: 1 }],
  inputFluids: [{ amount: 50, fluidGuid: WATER }],
  outputFluids: [{ amount: 25, fluidGuid: STEAM }],
  levelInheritance: { kind: "none", sourceInputIndex: -1, sourceItemIds: [], targetOutputIndex: -1, targetItemIds: [], maxCraftableLevel: 0 },
};

describe("MachineRecipeEntry fluids", () => {
  it("液体の入力・出力をアイテムの後ろに量付きで描き、クリック遷移は持たせない", () => {
    const onSelect = vi.fn();
    let renderer: ReactTestRenderer;
    act(() => {
      renderer = create(createElement(MachineRecipeEntry, { recipe, onSelect, testId: "entry" }));
    });

    const fluids = renderer!.root.findAllByType("mock-fluid-amount-slot" as never);
    expect(fluids.map((f) => [f.props.fluidGuid, f.props.badge])).toEqual([
      [WATER, { kind: "show", amount: 50 }],
      [STEAM, { kind: "show", amount: 25 }],
    ]);
    expect(fluids.map((f) => f.props.testId)).toEqual(["entry-input-fluid-0", "entry-output-fluid-0"]);
    expect(fluids.every((f) => f.props.onLeftDown === undefined)).toBe(true);
    expect(renderer!.root.findAllByType("mock-item-slot" as never)).toHaveLength(2);
  });
});
