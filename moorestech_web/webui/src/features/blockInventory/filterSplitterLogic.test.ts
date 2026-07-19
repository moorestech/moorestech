import { describe, expect, it } from "vitest";
import { filterSlotClickAction, modeLabel } from "./filterSplitterLogic";

describe("modeLabel", () => {
  it("3モードの表示キーを保持する", () => {
    expect(modeLabel).toEqual({
      default: "デフォルト",
      whitelist: "ホワイトリスト",
      blacklist: "ブラックリスト",
    });
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
