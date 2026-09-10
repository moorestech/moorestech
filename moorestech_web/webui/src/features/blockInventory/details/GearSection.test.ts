import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, GearDetailData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import GearSection from "./GearSection";

function render(gear: GearDetailData) {
  const data = { open: true, source: "block", blockType: "GearMachine", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], gear } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(GearSection, { data })); });
  return tree.root;
}

function textOf(root: ReturnType<typeof render>, testId: string): string {
  const node = root.findAll((n) => n.props["data-testid"] === testId)[0];
  return String(node.props.children);
}

describe("GearSection", () => {
  // 待機中（現在<基準相当）でも赤にならず、消費側は基準RPMを併記する
  // Even when idle (current below base), nothing turns insufficient; consumers show the base RPM
  it("renders consumed torque without a denominator and RPM with its base for consumers", () => {
    const root = render({ isClockwise: true, currentRpm: 5, currentTorque: 2.4, baseRpm: 10, role: "consumer" });
    expect(textOf(root, "gear-torque")).toBe('ui.blockInventory.gearConsumedTorque|{"value":"2.4"}');
    expect(textOf(root, "gear-rpm")).toBe('ui.blockInventory.gearRpmWithBase|{"current":"5.0","base":"10.0","value":"5.0"}');
    expect(root.findAll((n) => n.props["data-insufficient"] !== undefined)).toHaveLength(0);
  });
  it("renders generated torque and current RPM only for generators", () => {
    const root = render({ isClockwise: true, currentRpm: 20, currentTorque: 5, baseRpm: 0, role: "generator" });
    expect(textOf(root, "gear-torque")).toBe('ui.blockInventory.gearGeneratedTorque|{"value":"5.0"}');
    expect(textOf(root, "gear-rpm")).toBe('ui.blockInventory.gearRpmCurrent|{"current":"20.0","base":"0.0","value":"20.0"}');
  });
  // チェーンポール等の純伝達ブロックも同じ行構成（0.0がそのまま出る）
  // Pure transmission blocks such as chain poles share the same rows (0.0 is shown as is)
  it("keeps the same rows for a zero-torque transmission block", () => {
    const root = render({ isClockwise: true, currentRpm: 5, currentTorque: 0, baseRpm: 5, role: "consumer" });
    expect(textOf(root, "gear-torque")).toBe('ui.blockInventory.gearConsumedTorque|{"value":"0.0"}');
  });
});
