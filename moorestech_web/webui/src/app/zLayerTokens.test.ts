// Portal層の重なり順を数値の並びとして固定する
// Locks the portal layers' stacking order as a numeric sequence
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const tokens = readFileSync(new URL("./tokens.css", import.meta.url), "utf8");

function layer(name: string) {
  const match = tokens.match(new RegExp(`--z-${name}:\\s*(\\d+)`));
  if (match === null) throw new Error(`--z-${name} is missing from tokens.css`);
  return Number.parseInt(match[1], 10);
}

describe("z-layer tokens", () => {
  it("ツールチップはモーダルより前・トースト通知より後ろに立つ", () => {
    // tooltipがautoのままだとモーダル本文の裏へ回り、説明が読めなくなる
    // Leaving the tooltip at auto sends it behind modal content and the explanation becomes unreadable
    expect(layer("modal")).toBeLessThan(layer("tooltip"));
    expect(layer("tooltip")).toBeLessThan(layer("toast"));
  });

  it("stage内の常駐HUD層はscreen層より前に立つ", () => {
    expect(layer("screen")).toBeLessThan(layer("overlay-panel"));
  });
});
