import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen, CleanRoomDetailData } from "@/bridge";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string, params?: Record<string, string>) => `${key}|${JSON.stringify(params)}` }),
}));
vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import CleanRoomSection from "../CleanRoomSection";

function render(cleanRoom: CleanRoomDetailData | undefined) {
  const data = { open: true, source: "block", blockType: "CleanRoomMachine", identifier: "block:1", blockGuid: "g", itemSlots: [], fluidSlots: [], cleanRoom } as unknown as BlockInventoryOpen;
  let tree!: ReturnType<typeof create>;
  act(() => { tree = create(createElement(CleanRoomSection, { data })); });
  return tree.root;
}

function idsOf(root: ReturnType<typeof render>): string[] {
  return root.findAll((n) => typeof n.type === "string" && n.props["data-testid"] !== undefined).map((n) => n.props["data-testid"]);
}

describe("CleanRoomSection", () => {
  it("部屋欄が無ければ何も描かない", () => {
    expect(idsOf(render(undefined))).toEqual([]);
  });

  it("密閉済みの部屋は見出し・空気・清浄機・停止理由の順に描く", () => {
    const room = { headline: "classOut" as const, concentration: 1, airChangeRate: 0.5, volume: 74, airFilters: [{ x: 1, y: 33, z: 5, powerRatio: 1, filterItemCount: 0, filterRemainingRatio: 0 }] };
    const root = render({ fetchState: "fetched", kind: "enclosed", room, haltReason: "classOut" });
    expect(idsOf(root)).toEqual(["clean-room-section", "clean-room-headline", "clean-room-air", "clean-room-air-filter-0", "clean-room-halt-reason"]);
    expect(root.findByProps({ "data-testid": "clean-room-headline" }).props["data-insufficient"]).toBe(true);
    expect(root.findByProps({ "data-testid": "clean-room-halt-reason" }).props.children).toBe("ui.blockInventory.cleanRoomHaltClassOut|undefined");
  });

  it("清浄機が無い部屋はその旨を赤で出す", () => {
    const room = { headline: "class" as const, className: "A", concentration: 0, airChangeRate: 0, volume: 26, airFilters: [] };
    const root = render({ fetchState: "fetched", kind: "enclosed", room });
    expect(idsOf(root)).toEqual(["clean-room-section", "clean-room-headline", "clean-room-air", "clean-room-no-filter"]);
    expect(root.findByProps({ "data-testid": "clean-room-no-filter" }).props["data-insufficient"]).toBe(true);
  });

  it("隙間がある部屋は件数の見出しだけを赤で出す", () => {
    const root = render({ fetchState: "fetched", kind: "openings", openingCount: 2 });
    expect(idsOf(root)).toEqual(["clean-room-section", "clean-room-headline"]);
    expect(root.findByProps({ "data-testid": "clean-room-headline" }).props.children).toBe('ui.blockInventory.cleanRoomNotSealed|{"count":"2"}');
  });

  it("取得できていない間は欄の名前と取得状態の1行だけを描き、部屋の値を出さない", () => {
    const root = render({ fetchState: "transportFailed" });
    expect(idsOf(root)).toEqual(["clean-room-fetch-state"]);
    const row = root.findByProps({ "data-testid": "clean-room-fetch-state" });
    expect(row.props["data-fetch-state"]).toBe("transportFailed");
    expect(row.props.children).toBe('ui.blockInventory.blockInfoTransportFailed|{"target":"ui.blockInventory.cleanRoomInfoLabel|undefined"}');
  });
});
