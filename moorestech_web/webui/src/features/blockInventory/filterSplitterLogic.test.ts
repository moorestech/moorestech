import { describe, expect, it } from "vitest";
import { filterSlotClickAction, nextMode } from "./filterSplitterLogic";

describe("nextMode", () => {
  it("cycles default→whitelist→blacklist→default", () => {
    expect(nextMode("default")).toBe("whitelist");
    expect(nextMode("whitelist")).toBe("blacklist");
    expect(nextMode("blacklist")).toBe("default");
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
