import { describe, expect, it } from "vitest";
import { resolveDropAction } from "./hotbarDnd";

describe("resolveDropAction", () => {
  it("ビルドメニューエントリを枠へ落とすとassign", () => {
    expect(resolveDropAction({ kind: "buildMenuEntry", id: "guid-a" }, { kind: "hotbarSlot", index: 2 }))
      .toEqual({ type: "hotbar.assign", payload: { slot: 2, id: "guid-a" } });
  });
  it("枠から枠へ落とすとswap", () => {
    expect(resolveDropAction({ kind: "hotbarSlot", index: 1 }, { kind: "hotbarSlot", index: 4 }))
      .toEqual({ type: "hotbar.swap", payload: { from: 1, to: 4 } });
  });
  it("枠から枠外へ落とすとclear", () => {
    expect(resolveDropAction({ kind: "hotbarSlot", index: 1 }, { kind: "outside" }))
      .toEqual({ type: "hotbar.clear", payload: { slot: 1 } });
  });
});
