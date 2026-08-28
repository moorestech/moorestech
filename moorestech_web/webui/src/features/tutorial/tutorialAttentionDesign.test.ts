// 誘導表示の赤と脈動を単一の正にする検証
// Verifies the attention red and the pulse come from one source
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const tokens = read("../../app/tokens.css");
const keyHint = read("./keyControlHint.module.css");
const overlay = read("./overlay/style.module.css");
const worldPin = read("./worldPin.module.css");
const worldPinOverlay = read("./WorldPinOverlay.tsx");

describe("tutorial attention tokens", () => {
  it("原色赤・グロー・周期はtokensが唯一の正", () => {
    expect(tokens).toContain("--tutorial-attention-red: #ff0000");
    expect(tokens).toContain("--tutorial-attention-glow: rgb(255 0 0 / 24%)");
    expect(tokens).toContain("--tutorial-pulse-duration: 1200ms");
  });

  it("脈動キーフレームはtokensに1本だけ置き、静止側は常にscale(1)、振幅は利用側の変数で決める", () => {
    expect(tokens).toContain("@keyframes tutorial-attention-pulse");
    expect(tokens).toContain("100% { transform: scale(1); }");
    expect(tokens).toContain("transform: scale(var(--tutorial-pulse-scale))");
  });

  it("@keyframes tutorial-attention-pulseはtokens.cssに1本だけ存在する", () => {
    const combined = [tokens, keyHint, overlay, worldPin].join("\n");
    const definitions = combined.match(/@keyframes tutorial-attention-pulse\b/g) ?? [];
    expect(definitions).toHaveLength(1);
  });

  it("利用側は素名のanimation直書きを復活させない（CSS Modulesがハッシュ化しキーフレームに届かなくなる）", () => {
    for (const css of [keyHint, overlay, worldPin]) {
      expect(css).not.toMatch(/animation:\s*tutorial-attention-pulse\b/);
    }
  });

  it("脈動を参照するルールは必ず振幅変数--tutorial-pulse-scaleを同居させる", () => {
    for (const css of [keyHint, overlay, worldPin]) {
      if (/animation:\s*var\(--tutorial-pulse-name\)/.test(css)) {
        expect(css).toContain("--tutorial-pulse-scale:");
      }
    }
  });
});

describe("keyControl hint HUD", () => {
  it("文字色は原色赤トークンを指す", () => {
    expect(tokens).toContain("--tutorial-key-hint-color: var(--tutorial-attention-red)");
  });

  it("1.08の拡縮ループを共有キーフレームで持つ", () => {
    const hintRule = ruleBlock(keyHint, ".hint {");
    expect(hintRule).toContain("--tutorial-pulse-scale: 1.08");
    expect(hintRule).toContain("animation: var(--tutorial-pulse-name) var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it("機能側CSSに色と秒数を直書きしない", () => {
    assertNoRawColorOrDuration(keyHint);
  });

  it("共有様式keyHintTextの文字色は白のままで、インベントリ左下・研究左下を巻き込まない", () => {
    const shared = tokens.slice(tokens.indexOf(":where(.keyHintText) {"));
    expect(shared).toContain("color: var(--text-high-contrast)");
    expect(shared).not.toContain("--tutorial-attention-red");
    expect(shared).not.toContain("tutorial-attention-pulse");
  });
});

describe("tutorial highlight ring", () => {
  it("枠線とグローの両方が原色赤トークンを指し、旧来の黄が残らない", () => {
    expect(overlay).toContain("solid var(--tutorial-attention-red)");
    expect(overlay).toContain("var(--tutorial-attention-glow)");
    expect(overlay).not.toContain("#ffdd57");
    expect(overlay).not.toContain("255 221 87");
  });

  it("拡縮は1.03で、内側ノードを足さず既存の.highlight自身に付ける", () => {
    const rule = ruleBlock(overlay, ".highlight {");
    expect(rule).toContain("--tutorial-pulse-scale: 1.03");
    expect(rule).toContain("animation: var(--tutorial-pulse-name) var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it("ラベル面は脈動せず、既存のstage同率スケールを保つ", () => {
    const labelRule = ruleBlock(overlay, ".highlightLabel {");
    expect(labelRule).toContain("transform: scale(var(--ui-scale, 1))");
    expect(labelRule).not.toContain("tutorial-attention-pulse");
    expect(labelRule).not.toContain("--tutorial-attention-red");
  });

  it("機能側CSSに色リテラルと秒数リテラルを直書きしない", () => {
    assertNoRawColorOrDuration(overlay);
  });

  it("ドラッグガイド矢印は対象外で、移動ループのまま据え置く", () => {
    const dragRule = ruleBlock(overlay, ".dragGuide {");
    expect(dragRule).toContain("animation: drag-guide-loop var(--tutorial-drag-guide-duration) ease-in-out infinite");
    expect(dragRule).not.toContain("tutorial-attention-pulse");
  });
});

describe("world-pin off-screen arrow", () => {
  it("塗りは原色赤トークンで、世界分離用の縁取りは残す", () => {
    const svgRule = ruleBlock(worldPin, ".arrow svg {");
    expect(svgRule).toContain("fill: var(--tutorial-attention-red)");
    expect(svgRule).toContain("stroke: var(--world-pin-face)");
  });

  it("脈動はsvg側に付け、1.08で回す", () => {
    const svgRule = ruleBlock(worldPin, ".arrow svg {");
    expect(svgRule).toContain("--tutorial-pulse-scale: 1.08");
    expect(svgRule).toContain("animation: var(--tutorial-pulse-name) var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it(".arrow div側にはanimationもtransformも付けない（インラインtransformを潰さないため）", () => {
    const divRule = ruleBlock(worldPin, ".arrow {");
    expect(divRule).not.toContain("animation");
    expect(divRule).not.toContain("transform");
    expect(divRule).toContain("position: fixed");
  });

  it("矢印の位置と回転はTSXのインラインtransformが持ち続ける", () => {
    expect(worldPinOverlay).toContain("translate(-50%, -50%) rotate(${angle}deg) scale(var(--ui-scale, 1))");
  });

  it("機能側CSSに色リテラルと秒数リテラルを直書きしない", () => {
    assertNoRawColorOrDuration(worldPin);
  });

  it("ピン本体のラベル・マーカーは据え置く", () => {
    const markerRule = worldPin.slice(worldPin.indexOf(".marker {"), worldPin.indexOf(".arrow {"));
    expect(markerRule).toContain("fill: var(--world-pin-face)");
    expect(markerRule).not.toContain("tutorial-attention");
  });
});

// 指定セレクタの宣言ブロックだけを切り出す。無ければその場で失敗させ、-1を位置として使わない
// Slices only the given selector's declaration block; fails loudly when absent instead of using -1 as a position
function ruleBlock(css: string, selector: string): string {
  const start = css.indexOf(selector);
  expect(start, `${selector} が見つからない`).toBeGreaterThanOrEqual(0);
  const end = css.indexOf("}", start);
  expect(end, `${selector} の宣言ブロックが閉じていない`).toBeGreaterThan(start);
  return css.slice(start, end);
}

// コメント除去後、色・秒数の生リテラル不在を検査
// Checks declarations, after stripping comments, for raw color/duration literals
function assertNoRawColorOrDuration(css: string): void {
  const withoutComments = css.replace(/\/\*[\s\S]*?\*\//g, "");
  expect(withoutComments).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
  expect(withoutComments).not.toMatch(/\d+m?s\b/);
}

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
