// hudの見た目契約を固定する
// Locks the hud variant's visual contract
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const component = read("./index.tsx");
const style = read("./style.module.css");
const hudVariantStyle = read("./hudVariant.module.css");
const tokens = read("../../../app/tokens.css");

describe("GamePanel hud variant", () => {
  it("variantの型とクラスマップにhudを持つ", () => {
    expect(component).toContain('export type GamePanelVariant = "default" | "craft" | "skit" | "hud"');
    expect(component).toContain("Record<GamePanelVariant, string>");
    expect(component).toContain("hud: hudVariantStyles.hud");
  });

  it("hudの面色はパネル面と同じ基底トークンから引き、フェード幅は共通トークンを使う", () => {
    expect(tokens).toContain("--surface-navy: rgb(10 14 27 / 80%)");
    expect(tokens).toContain("--hud-panel-face: var(--surface-navy)");
    expect(style).toContain("var(--surface-navy) 11.742px, var(--surface-navy) 100%");
    expect(tokens).toContain("--hud-panel-edge-fade: var(--panel-edge-fade)");
    expect(tokens).toContain("--hud-panel-padding: 20px");
  });

  it("hudの面は4辺を固定長でフェードする", () => {
    const hudFace = hudVariantStyle.slice(hudVariantStyle.indexOf('[data-variant="hud"].hud::before'));
    expect(hudFace).toContain("background: var(--hud-panel-face)");
    expect(hudFace).toContain("90deg, transparent 0, #000 var(--hud-panel-edge-fade)");
    expect(hudFace).toContain("180deg, transparent 0, #000 var(--hud-panel-edge-fade)");
    expect(hudFace).toContain("mask-composite: intersect");
  });

  it("既定面は肯定形のvariant判定で敷き、重なり順は全variant共通にする", () => {
    // 除外連鎖の食い違いで.bodyが面の裏へ沈む罠を、肯定形で構造的に潰す
    // Positive-form predicates remove the trap where a mismatched :not() chain sinks .body behind the face
    expect(style).toContain('.panel[data-variant="default"]::before');
    expect(style).toContain(".panel > *:not(.bottomDeco)");
    expect(style).not.toContain(":not(.craft)");
  });

  it("hudは罫線・三角・グリップの装飾を持たない", () => {
    const hudRules = hudVariantStyle.slice(hudVariantStyle.indexOf('[data-variant="hud"].hud {'));
    expect(hudRules).not.toContain("decoLine");
    expect(hudRules).not.toContain("bottomDeco");
    expect(hudRules).not.toContain("clip-path");
    expect(hudRules).not.toContain("box-shadow");
  });

  it("hudは文字色をGamePanelから受け取らず利用側から継承する", () => {
    // .panelの既定色がHUD本文の--text-high-contrastを潰す退行を止める
    // Stops .panel's default color from overriding the HUD body's --text-high-contrast
    const hudPadding = hudVariantStyle.slice(
      hudVariantStyle.indexOf('[data-variant="hud"].hud {'),
      hudVariantStyle.indexOf('[data-variant="hud"].hud::before'));
    expect(hudPadding).toContain("color: inherit");
  });

  it("hudのpaddingセレクタは.panelより詳細度で勝ち、import順に依存しない", () => {
    // .panel(style.module.css)は単純クラス(0,1,0)のため、hud側は属性+クラスの複合(0,2,0)で確実に上回る
    // .panel (style.module.css) is a plain class (0,1,0); pair an attribute selector with the class so hud always wins at (0,2,0)
    expect(hudVariantStyle).toContain('[data-variant="hud"].hud {\n  padding: var(--hud-panel-padding);');
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
