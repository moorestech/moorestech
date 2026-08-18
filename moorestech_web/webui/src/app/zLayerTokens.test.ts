// Portal層の重なり順を数値の並びとして固定する
// Locks the portal layers' stacking order as a numeric sequence
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const tokens = readFileSync(new URL("./tokens.css", import.meta.url), "utf8");
const appStyles = readFileSync(new URL("./App.module.css", import.meta.url), "utf8");

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

  it("通知が沈む背面層はディムより前・stageより後ろに立つ", () => {
    // 背面層がディムより後ろだと暗転に飲まれ、stageより前だと画面UIに被さる
    // Behind the dim it drowns in the darkening; ahead of the stage it covers the screen UI
    expect(layer("app-backdrop")).toBeLessThan(layer("behind-stage"));
    expect(layer("behind-stage")).toBeLessThan(layer("stage"));
  });

  it("stageとディムの層序は生値でなくトークンを参照する", () => {
    // 生値のままだと通知側CSSから層序を参照できず、DOM順への暗黙依存へ戻る
    // Raw values leave the notification CSS unable to reference the order, regressing to implicit DOM-order reliance
    expect(appStyles).toContain("z-index: var(--z-stage)");
    expect(appStyles).toContain("z-index: var(--z-app-backdrop)");
  });
});
