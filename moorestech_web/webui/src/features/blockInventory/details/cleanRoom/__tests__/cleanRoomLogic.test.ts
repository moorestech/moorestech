import { describe, expect, it } from "vitest";
import type { CleanRoomSummaryData } from "@/bridge";
import { L } from "@/shared/i18n";
import { cleanRoomDisplay } from "../cleanRoomLogic";

const room: CleanRoomSummaryData = {
  headline: "class",
  className: "A",
  concentration: 0.12345,
  airChangeRate: 2.5,
  volume: 74,
  airFilters: [{ x: 1, y: 33, z: 5, powerRatio: 0.756, filterItemCount: 5, filterRemainingRatio: 0.5 }],
};

describe("cleanRoomDisplay", () => {
  it("クラスに届いた部屋はクラス名・濃度・換気回数・清浄機ごとの行を出す", () => {
    const view = cleanRoomDisplay({ kind: "enclosed", room });
    expect(view.headline).toEqual({ key: L.ui.blockInventory.cleanRoomClass, params: { className: "A" } });
    expect(view.headlineInsufficient).toBe(false);
    expect(view.air).toEqual({ key: L.ui.blockInventory.cleanRoomAir, params: { concentration: "0.123", airChangeRate: "2.500" } });
    expect(view.filters).toEqual([{ key: L.ui.blockInventory.cleanRoomFilter, params: { index: "1", power: "76", count: "5", remaining: "50" } }]);
    expect(view.noFilter).toBeNull();
    expect(view.haltReason).toBeNull();
  });

  it("Outの部屋は赤で示し、清浄機が無ければその旨を出す", () => {
    const outRoom: CleanRoomSummaryData = { headline: "classOut", concentration: 3, airChangeRate: 0, volume: 74, airFilters: [] };
    const view = cleanRoomDisplay({ kind: "enclosed", room: outRoom, haltReason: "classOut" });
    expect(view.headline).toEqual({ key: L.ui.blockInventory.cleanRoomClassOut });
    expect(view.headlineInsufficient).toBe(true);
    expect(view.noFilter).toEqual({ key: L.ui.blockInventory.cleanRoomNoFilter });
    expect(view.haltReason).toEqual({ key: L.ui.blockInventory.cleanRoomHaltClassOut });
  });

  it.each([
    [{ kind: "openings", openingCount: 3 } as const, { key: L.ui.blockInventory.cleanRoomNotSealed, params: { count: "3" } }, true],
    [{ kind: "noEnclosure" } as const, { key: L.ui.blockInventory.cleanRoomNoEnclosure }, true],
    [{ kind: "tooLarge", maxRoomVolume: 4096 } as const, { key: L.ui.blockInventory.cleanRoomTooLarge, params: { max: "4096" } }, true],
    [{ kind: "detecting" } as const, { key: L.ui.blockInventory.cleanRoomDetecting }, false],
  ])("未密閉 %j は見出しだけを出す", (cleanRoom, headline, insufficient) => {
    const view = cleanRoomDisplay({ ...cleanRoom, haltReason: "notEnclosed" });
    expect(view.headline).toEqual(headline);
    expect(view.headlineInsufficient).toBe(insufficient);
    expect(view.air).toBeNull();
    expect(view.filters).toEqual([]);
    expect(view.haltReason).toEqual({ key: L.ui.blockInventory.cleanRoomHaltNotEnclosed });
  });
});
