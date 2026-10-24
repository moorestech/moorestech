import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, CleanRoomYieldData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import CleanRoomYieldSection from "../CleanRoomYieldSection";

function render(cleanRoomYield: CleanRoomYieldData | undefined) {
  const data = { open: true, source: "block", blockType: "CleanRoomMachine", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], cleanRoomYield } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(CleanRoomYieldSection, { data })); });
  return tree.root;
}

function idsOf(root: ReturnType<typeof render>): string[] {
  return root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
}

describe("CleanRoomYieldSection", () => {
  it("抽選の中身が無ければ何も描かない", () => {
    expect(idsOf(render(undefined))).toEqual([]);
  });

  it("成膜は上限グレードと昇格率の行だけを描く", () => {
    const root = render({ role: "waferGrade", maxGrade: 2, promotionChance: 0.9 });
    expect(idsOf(root)).toEqual(["clean-room-yield-section", "clean-room-yield-max-grade", "clean-room-yield-promotion-chance"]);
    expect(root.findByProps({ "data-testid": "clean-room-yield-max-grade" }).props.children).toBe('ui.blockInventory.cleanRoomYieldMaxGrade|{"grade":"2"}');
  });

  it("EUV は層の失敗率の行だけを描く", () => {
    expect(idsOf(render({ role: "layerExposure", layerFailRate: 0.02 }))).toEqual(["clean-room-yield-section", "clean-room-yield-layer-fail-rate"]);
  });

  it("ダイシングはダイの損失率の行だけを描く", () => {
    const root = render({ role: "dicing", dieLossRate: 0.03 });
    expect(idsOf(root)).toEqual(["clean-room-yield-section", "clean-room-yield-die-loss-rate"]);
    expect(root.findByProps({ "data-testid": "clean-room-yield-die-loss-rate" }).props.children).toBe('ui.blockInventory.cleanRoomYieldDieLossRate|{"percent":"3.0"}');
  });
});
