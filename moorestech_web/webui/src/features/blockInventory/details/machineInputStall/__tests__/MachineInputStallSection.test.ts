import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { MachineInputStallData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
  useItemDisplayName: () => (itemId: number) => `item${itemId}`,
}));
vi.mock("@mantine/core", () => ({
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import MachineInputStallSection from "../MachineInputStallSection";

function render(stall: MachineInputStallData) {
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(MachineInputStallSection, { stall })); });
  return tree.root;
}

describe("MachineInputStallSection", () => {
  it("レベル違いは品の名前を差し込んで赤字で描く", () => {
    const row = render({ fetchState: "fetched", reason: "mixedLevel", itemId: 12 }).findByProps({ "data-testid": "machine-input-stall" });
    expect(row.props["data-insufficient"]).toBe(true);
    expect(row.props.children).toBe('ui.blockInventory.machineInputStallMixedLevel|{"itemName":"item12"}');
  });

  it("上限超えは製作上限の文言で描く", () => {
    const row = render({ fetchState: "fetched", reason: "aboveCraftCap", itemId: 7 }).findByProps({ "data-testid": "machine-input-stall" });
    expect(row.props.children).toBe('ui.blockInventory.machineInputStallAboveCraftCap|{"itemName":"item7"}');
  });

  it("取得できていない間は赤字にせず取得状態の1行だけを描く", () => {
    const root = render({ fetchState: "unsupportedTarget" });
    expect(root.findAll((n) => n.props["data-testid"] === "machine-input-stall")).toEqual([]);
    const row = root.findByProps({ "data-testid": "machine-input-stall-fetch-state" });
    expect(row.props["data-insufficient"]).toBeUndefined();
    expect(row.props.children).toBe('ui.blockInventory.blockInfoUnsupportedTarget|{"target":"ui.blockInventory.machineInputStallInfoLabel|undefined"}');
  });
});
