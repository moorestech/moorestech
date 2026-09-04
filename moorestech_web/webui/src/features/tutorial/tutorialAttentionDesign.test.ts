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
  it("原色赤と周期はtokensが唯一の正", () => {
    expect(tokens).toContain("--tutorial-attention-red: #ff0000");
    expect(tokens).toContain("--tutorial-pulse-duration: 1200ms");
  });

  it("グローは赤から導出し、色の決定を二重に持たない", () => {
    expect(tokens).toContain("--tutorial-attention-glow: rgb(from var(--tutorial-attention-red) r g b / 24%)");
    expect(tokens).not.toMatch(/--tutorial-attention-glow:\s*rgb\(\s*\d/);
  });

  it("脈動キーフレームは振幅を実数値で焼き、var()参照を残さない", () => {
    const strong = keyframesBlock(tokens, "tutorial-attention-pulse-strong");
    const subtle = keyframesBlock(tokens, "tutorial-attention-pulse-subtle");
    expect(strong).toContain("100% { transform: scale(1); }");
    expect(strong).toContain("50% { transform: scale(1.08); }");
    expect(subtle).toContain("100% { transform: scale(1); }");
    expect(subtle).toContain("50% { transform: scale(1.03); }");
    expect(tokens).not.toContain("scale(var(--tutorial-pulse-scale))");
  });

  it("脈動キーフレームはtokens.cssにだけ存在し、機能側へ複製されない", () => {
    const combined = [tokens, keyHint, overlay, worldPin].join("\n");
    expect(combined.match(/@keyframes tutorial-attention-pulse-strong\b/g) ?? []).toHaveLength(1);
    expect(combined.match(/@keyframes tutorial-attention-pulse-subtle\b/g) ?? []).toHaveLength(1);
  });

  it("利用側は素名のanimation直書きを復活させない（CSS Modulesがハッシュ化しキーフレームに届かなくなる）", () => {
    for (const css of [keyHint, overlay, worldPin]) {
      expect(css).not.toMatch(/animation:\s*tutorial-attention-pulse\b/);
    }
  });

  it("利用側は振幅を持たず、tokensの名前トークンだけを参照する", () => {
    for (const css of [keyHint, overlay, worldPin]) {
      expect(css).not.toContain("--tutorial-pulse-scale");
    }
  });
});

describe("keyControl hint HUD", () => {
  it("文字色は原色赤トークンを指す", () => {
    expect(tokens).toContain("--tutorial-key-hint-color: var(--tutorial-attention-red)");
  });

  it("1.08の拡縮ループを共有キーフレームで持つ", () => {
    const hintRule = ruleBlock(keyHint, ".hint {");
    expect(hintRule).toContain("animation: var(--tutorial-pulse-strong) var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it("機能側CSSに色と秒数を直書きしない", () => {
    assertNoRawColorOrDuration(keyHint);
  });

  it("共有様式keyHintTextの文字色は白のままで、インベントリ左下・研究左下を巻き込まない", () => {
    // 本体とkbdを別ブロックで検査し、片方の宣言でもう片方の欠落を隠さない
    // Checks the base and kbd blocks separately so one declaration cannot mask the other's absence
    for (const block of [ruleBlock(tokens, ":where(.keyHintText) {"), ruleBlock(tokens, ":where(.keyHintText) kbd {")]) {
      expect(block).toContain("color: var(--text-high-contrast)");
      // 素名だけでなく間接トークン経由の赤・脈動も塞ぐ（実際の適用は必ずvar()経由のため）
      // Blocks the red and the pulse through their indirection tokens too, since features always apply them via var()
      expect(block).not.toContain("--tutorial-attention-red");
      expect(block).not.toContain("--tutorial-key-hint-color");
      expect(block).not.toMatch(/tutorial-attention-pulse|--tutorial-pulse-(strong|subtle)/);
      expect(block).not.toContain("animation");
      expect(block).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
    }
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
    expect(rule).toContain("animation: var(--tutorial-pulse-subtle) var(--tutorial-pulse-duration) ease-in-out infinite");
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
    expect(svgRule).toContain("animation: var(--tutorial-pulse-strong) var(--tutorial-pulse-duration) ease-in-out infinite");
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
    const markerRule = ruleBlock(worldPin, ".marker {");
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

// キーフレーム本体を取り出す。ステップ自身が波括弧を持つため行頭の閉じ括弧で終端する
// Extracts a keyframes body, ending at the line-start brace since the steps carry braces of their own
function keyframesBlock(css: string, name: string): string {
  const start = css.indexOf(`@keyframes ${name} {`);
  expect(start, `@keyframes ${name} が見つからない`).toBeGreaterThanOrEqual(0);
  const end = css.indexOf("\n}", start);
  expect(end, `@keyframes ${name} が閉じていない`).toBeGreaterThan(start);
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
