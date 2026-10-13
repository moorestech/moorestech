import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import GearToElectricSection from "../GearToElectricSection";

function render(gearToElectric?: NonNullable<BlockInventoryOpen["gearToElectric"]>) {
  const data = { open: true, source: "block", blockType: "GearToElectricGenerator", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], gearToElectric } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(GearToElectricSection, { data })); });
  return tree;
}

function textOf(tree: ReturnType<typeof render>, testId: string): string {
  const node = tree.root.findAll((n) => n.props["data-testid"] === testId)[0];
  return String(node.props.children);
}

describe("GearToElectricSection", () => {
  it("shows supplied power over the master maximum while generating", () => {
    const tree = render({ generationState: "generating", suppliedPower: 72.4, maxGeneratedPower: 100 });
    expect(textOf(tree, "gear-to-electric-state")).toBe("ui.blockInventory.gearToElectricStateGenerating|undefined");
    expect(textOf(tree, "gear-to-electric-supplied-power")).toBe('ui.blockInventory.gearToElectricSuppliedPower|{"current":"72","max":"100"}');
    expect(tree.root.findAll((n) => n.props["data-insufficient"] === true)).toHaveLength(0);
  });
  it("shows idle without an insufficient tone", () => {
    const tree = render({ generationState: "idle", suppliedPower: 0, maxGeneratedPower: 100 });
    expect(textOf(tree, "gear-to-electric-state")).toBe("ui.blockInventory.gearToElectricStateIdle|undefined");
    expect(tree.root.findAll((n) => n.props["data-insufficient"] === true)).toHaveLength(0);
  });
  it("shows stopped with an insufficient tone", () => {
    const tree = render({ generationState: "stopped", suppliedPower: 0, maxGeneratedPower: 100 });
    expect(textOf(tree, "gear-to-electric-state")).toBe("ui.blockInventory.gearToElectricStateStopped|undefined");
    expect(tree.root.findAll((n) => n.props["data-insufficient"] === true).length).toBeGreaterThan(0);
  });
  it.each([[0, "0"], [0.1, "0.10"], [0.49, "0.49"], [0.5, "1"], [29.49999, "29"], [29.5, "30"], [1e-8, "1.0e-8"]])("preserves positive supply %s as %s", (suppliedPower, expected) => {
    const tree = render({ generationState: "generating", suppliedPower: Number(suppliedPower), maxGeneratedPower: 100 });
    expect(textOf(tree, "gear-to-electric-supplied-power")).toBe(`ui.blockInventory.gearToElectricSuppliedPower|{"current":"${expected}","max":"100"}`);
  });
  it("preserves a positive master maximum below integer precision", () => {
    const tree = render({ generationState: "generating", suppliedPower: 0.1, maxGeneratedPower: 0.2 });
    expect(textOf(tree, "gear-to-electric-supplied-power")).toBe('ui.blockInventory.gearToElectricSuppliedPower|{"current":"0.10","max":"0.20"}');
  });
  it("explains the measurement timing on the state row", () => {
    const tree = render({ generationState: "generating", suppliedPower: 10, maxGeneratedPower: 100 });
    expect(tree.root.findAll((n) => n.props["data-testid"] === "gear-to-electric-state")[0].props.title).toBe("ui.blockInventory.gearToElectricMeasurementTiming|undefined");
  });
  it("renders nothing before detail arrives", () => {
    expect(render().toJSON()).toBeNull();
  });
});
