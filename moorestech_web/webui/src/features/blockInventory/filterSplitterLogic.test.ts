import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import { filterModeTranslationKey, filterSlotClickAction } from "./filterSplitterLogic";

describe("filterModeTranslationKey", () => {
  it("3モードを型付き翻訳キーへ対応づける", () => {
    expect(filterModeTranslationKey("default")).toBe(L.ui.blockInventory.filterDefault);
    expect(filterModeTranslationKey("whitelist")).toBe(L.ui.blockInventory.filterWhitelist);
    expect(filterModeTranslationKey("blacklist")).toBe(L.ui.blockInventory.filterBlacklist);
  });
});

describe("filterSlotClickAction", () => {
  it("grabCount=0 かつ clear=false は noop", () => {
    expect(filterSlotClickAction(0, false)).toBe("noop");
  });

  it("grabCount>0 かつ clear=false は set", () => {
    expect(filterSlotClickAction(1, false)).toBe("set");
  });

  it("clear=true は grabCount に関わらず clear", () => {
    expect(filterSlotClickAction(0, true)).toBe("clear");
    expect(filterSlotClickAction(1, true)).toBe("clear");
  });
});
