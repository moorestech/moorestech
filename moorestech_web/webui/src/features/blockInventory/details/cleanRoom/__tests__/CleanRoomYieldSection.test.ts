import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { CleanRoomYieldData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import CleanRoomYieldSection from "../CleanRoomYieldSection";

function render(cleanRoomYield: CleanRoomYieldData) {
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(CleanRoomYieldSection, { cleanRoomYield })); });
  return tree.root;
}

function idsOf(root: ReturnType<typeof render>): string[] {
  return root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
}

describe("CleanRoomYieldSection", () => {
  it("EUV は層の失敗率の行だけを描く", () => {
    expect(idsOf(render({ fetchState: "fetched", role: "layerExposure", layerFailRate: 0.02 }))).toEqual(["clean-room-yield-section", "clean-room-yield-layer-fail-rate"]);
  });

  it("ダイシングは品質Tier・損失率・0でないレベル割合・失敗割合の行を描く", () => {
    const root = render({ fetchState: "fetched", role: "dicing", dieLossRate: 0.03, qualityTier: 1, levelRates: [0.7, 0.1, 0], failedRate: 0.2 });
    expect(idsOf(root)).toEqual(["clean-room-yield-section", "clean-room-yield-quality-tier", "clean-room-yield-die-loss-rate", "clean-room-yield-level-rate-1", "clean-room-yield-level-rate-2", "clean-room-yield-failed-rate"]);
    expect(root.findByProps({ "data-testid": "clean-room-yield-quality-tier" }).props.children).toBe('ui.blockInventory.cleanRoomYieldQualityTier|{"tier":"1"}');
    expect(root.findByProps({ "data-testid": "clean-room-yield-level-rate-2" }).props.children).toBe('ui.blockInventory.cleanRoomYieldLevelRate|{"level":"2","percent":"10.0"}');
  });

  it("読めない応答の間は取得状態の1行だけを描き、率を出さない", () => {
    const root = render({ fetchState: "unreadable" });
    const ids = root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
    expect(ids).toEqual(["clean-room-yield-fetch-state"]);
    expect(root.findByProps({ "data-testid": "clean-room-yield-fetch-state" }).props.children).toBe('ui.blockInventory.blockInfoUnreadable|{"target":"ui.blockInventory.cleanRoomYieldInfoLabel|undefined"}');
  });
});
