import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import { blockInfoUnavailableKey } from "../blockInfoFetchStateLogic";

describe("blockInfoUnavailableKey", () => {
  it("取得できていない状態ごとに別の文言を選ぶ", () => {
    expect(blockInfoUnavailableKey({ fetchState: "notFetched" })).toBe(L.ui.blockInventory.blockInfoNotFetched);
    expect(blockInfoUnavailableKey({ fetchState: "transportFailed" })).toBe(L.ui.blockInventory.blockInfoTransportFailed);
    expect(blockInfoUnavailableKey({ fetchState: "unreadable" })).toBe(L.ui.blockInventory.blockInfoUnreadable);
    expect(blockInfoUnavailableKey({ fetchState: "unsupportedTarget" })).toBe(L.ui.blockInventory.blockInfoUnsupportedTarget);
    expect(blockInfoUnavailableKey({ fetchState: "vanished" })).toBe(L.ui.blockInventory.blockInfoVanished);
  });
});
