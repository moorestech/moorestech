import { describe, expect, it } from "vitest";
import { clickOutcome, nextRevealCount, shouldRevealImmediately } from "./interaction";

describe("blocking skit interaction", () => {
  it("reveals the full body without dispatching advance while typing", () => {
    expect(clickOutcome(3, 10, true)).toBe("reveal");
  });

  it("dispatches advance only after the full body is visible", () => {
    expect(clickOutcome(10, 10, true)).toBe("advance");
  });

  it("does nothing when advance is not allowed", () => {
    expect(clickOutcome(10, 10, false)).toBe("none");
  });

  it("reveals one Unicode code point per tick", () => {
    expect(nextRevealCount("A😊B", 1)).toBe(2);
    expect(Array.from("A😊B").slice(0, nextRevealCount("A😊B", 1)).join("")).toBe("A😊");
  });

  it("reveals reconnect snapshots immediately and animates later events", () => {
    expect(shouldRevealImmediately("open", "restoring", "typewriter", 50)).toBe(true);
    expect(shouldRevealImmediately("open", "open", "typewriter", 50)).toBe(false);
  });
});
