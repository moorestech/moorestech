// hud variantが面と境界フェードだけを持ち、罫線・三角・グリップを持たないことを固定する
// Locks the hud variant to a face and boundary fade, without rules, triangles, or a grip
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const component = read("./index.tsx");
const style = read("./style.module.css");
const tokens = read("../../../app/tokens.css");

describe("GamePanel hud variant", () => {
  it("variantの型とクラスマップにhudを持つ", () => {
    expect(component).toContain('variant?: "default" | "craft" | "skit" | "hud"');
    expect(component).toContain("hud: styles.hud");
  });

  it("hudの面色はパネル面と同値でフェード幅は共通トークンを使う", () => {
    expect(tokens).toContain("--hud-panel-face: rgb(10 14 27 / 80%)");
    expect(tokens).toContain("--hud-panel-edge-fade: var(--panel-edge-fade)");
    expect(tokens).toContain("--hud-panel-padding: 20px");
  });

  it("hudの面は4辺を固定長でフェードする", () => {
    const hudFace = style.slice(style.indexOf(".hud::before"));
    expect(hudFace).toContain("background: var(--hud-panel-face)");
    expect(hudFace).toContain("90deg, transparent 0, #000 var(--hud-panel-edge-fade)");
    expect(hudFace).toContain("180deg, transparent 0, #000 var(--hud-panel-edge-fade)");
    expect(hudFace).toContain("mask-composite: intersect");
  });

  it("既定面のフェード合成からhudを除外する", () => {
    expect(style).toContain(".panel:not(.craft):not(.skit):not(.hud)::before");
  });

  it("hudは罫線・三角・グリップの装飾を持たない", () => {
    const hudRules = style.slice(style.indexOf(".hud {"));
    expect(hudRules).not.toContain("decoLine");
    expect(hudRules).not.toContain("bottomDeco");
    expect(hudRules).not.toContain("clip-path");
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
