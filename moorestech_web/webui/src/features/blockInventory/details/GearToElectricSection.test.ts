import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, GearToElectricData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import GearToElectricSection from "./GearToElectricSection";

function render(gearToElectric?: GearToElectricData) {
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
  it("renders nothing before detail arrives", () => {
    expect(render().toJSON()).toBeNull();
  });
});
