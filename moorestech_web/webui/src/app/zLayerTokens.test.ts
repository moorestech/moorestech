// Portal層の重なり順を数値の並びとして固定する
// Locks the portal layers' stacking order as a numeric sequence
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const tokens = readFileSync(new URL("./tokens.css", import.meta.url), "utf8");
const appStyles = readFileSync(new URL("./App.module.css", import.meta.url), "utf8");

// 群ごとに専用ヘルパーへ分け、接頭辞をまたぐ比較をそもそも書けないようにする
// A dedicated helper per group makes it impossible to even write a cross-prefix comparison
function makeLayerReader(prefix: "viewport" | "stage" | "portal") {
  return (name: string) => {
    const fullName = `${prefix}-${name}`;
    const match = tokens.match(new RegExp(`--z-${fullName}:\\s*(\\d+)`));
    if (match === null) throw new Error(`--z-${fullName} is missing from tokens.css`);
    return Number.parseInt(match[1], 10);
  };
}

const viewportLayer = makeLayerReader("viewport");
const stageLayer = makeLayerReader("stage");
const portalLayer = makeLayerReader("portal");

describe("z-layer tokens: .viewport直下", () => {
  it("通知が沈む背面層はディムより前・stageより後ろに立つ", () => {
    // 背面層がディムより後ろだと暗転に飲まれ、stageより前だと画面UIに被さる
    // Behind the dim it drowns in the darkening; ahead of the stage it covers the screen UI
    expect(viewportLayer("backdrop")).toBeLessThan(viewportLayer("behind-stage"));
    expect(viewportLayer("behind-stage")).toBeLessThan(viewportLayer("stage"));
  });

  it("stageとディムの層序は生値でなくトークンを参照する", () => {
    // 生値のままだと通知側CSSから層序を参照できず、DOM順への暗黙依存へ戻る
    // Raw values leave the notification CSS unable to reference the order, regressing to implicit DOM-order reliance
    expect(appStyles).toContain("z-index: var(--z-viewport-stage)");
    expect(appStyles).toContain("z-index: var(--z-viewport-backdrop)");
  });
});

describe("z-layer tokens: .stage内部", () => {
  it("常駐HUD層はscreen層より前に立ち、ツールチップはさらに前に立つ", () => {
    // stageは独自スタッキングコンテキストなので、この群の比較はstage内部でのみ意味を持つ
    // .stage owns its own stacking context, so this comparison is only meaningful inside .stage
    expect(stageLayer("screen")).toBeLessThan(stageLayer("overlay-panel"));
    // 全域パネルの上へ出す常設面は常駐HUD層より前・ツールチップより後ろに挟まる
    // The chrome above full-stage panels wedges ahead of the always-on HUD layer and behind the tooltip
    expect(stageLayer("overlay-panel")).toBeLessThan(stageLayer("overlay-panel-chrome"));
    expect(stageLayer("overlay-panel-chrome")).toBeLessThan(stageLayer("tooltip"));
  });
});

describe("z-layer tokens: body直下Portal", () => {
  it("モーダルはトーストより前に立つ", () => {
    // Portal各要素はbody直下の兄弟としてroot文脈で直接競うため、この群の比較は数値どおり意味を持つ
    // Every Portal element is a body-level sibling competing directly in the root context, so this comparison is meaningful as-is
    expect(portalLayer("modal")).toBeLessThan(portalLayer("toast"));
  });

  it("出展モードの言語選択ゲートは再接続オーバーレイより前に立つ", () => {
    // ゲート待機中にWSが切れると再接続オーバーレイが被さり、言語ボタンが押せなくなる
    // A WS drop during the wait would cover the language buttons with the reconnect overlay
    expect(portalLayer("reconnect")).toBeLessThan(portalLayer("event-language-gate"));
  });
});
