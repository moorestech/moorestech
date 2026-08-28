// 誘導表示の赤と脈動が単一の値源から来ていることを検証する
// Verifies the attention red and the pulse come from one source
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const tokens = read("../../app/tokens.css");
const keyHint = read("./keyControlHint.module.css");

describe("tutorial attention tokens", () => {
  it("原色赤・グロー・周期はtokensが唯一の正", () => {
    expect(tokens).toContain("--tutorial-attention-red: #ff0000");
    expect(tokens).toContain("--tutorial-attention-glow: rgb(255 0 0 / 24%)");
    expect(tokens).toContain("--tutorial-pulse-duration: 1200ms");
  });

  it("脈動キーフレームはtokensに1本だけ置き、振幅は利用側の変数で決める", () => {
    expect(tokens).toContain("@keyframes tutorial-attention-pulse");
    expect(tokens).toContain("transform: scale(var(--tutorial-pulse-scale))");
  });
});

describe("keyControl hint HUD", () => {
  it("文字色は原色赤トークンを指す", () => {
    expect(tokens).toContain("--tutorial-key-hint-color: var(--tutorial-attention-red)");
  });

  it("1.08の拡縮ループを共有キーフレームで持つ", () => {
    expect(keyHint).toContain("--tutorial-pulse-scale: 1.08");
    expect(keyHint).toContain("animation: tutorial-attention-pulse var(--tutorial-pulse-duration) ease-in-out infinite");
  });

  it("機能側CSSに色と秒数を直書きしない", () => {
    expect(keyHint).not.toMatch(/#[0-9a-fA-F]{3,8}\b/);
    expect(keyHint).not.toMatch(/\d+m?s\b/);
  });

  it("共有様式keyHintTextの文字色は白のままで、インベントリ左下・研究左下を巻き込まない", () => {
    const shared = tokens.slice(tokens.indexOf(":where(.keyHintText) {"));
    expect(shared).toContain("color: var(--text-high-contrast)");
    expect(shared).not.toContain("--tutorial-attention-red");
    expect(shared).not.toContain("tutorial-attention-pulse");
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
