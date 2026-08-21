// hudの見た目契約のうち、e2eの実描画では読めない「供給元」だけを固定する
// Locks only what the rendered e2e cannot read: where the hud variant's face comes from
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const style = read("./style.module.css");
const hudVariantStyle = read("./hudVariant.module.css");
const tokens = read("../../../app/tokens.css");

describe("GamePanel hud variant", () => {
  it("hudの面色とフェード幅はパネル面と同じ基底トークンを直接引く", () => {
    // 別名トークンを挟むと値が同じまま二重管理になり、片側だけ動いても誰も気付かない
    // An alias token duplicates ownership of the same value, so a one-sided change goes unnoticed
    expect(tokens).toContain("--surface-navy: rgb(10 14 27 / 80%)");
    expect(style).toContain("var(--surface-navy) 11.742px, var(--surface-navy) 100%");
    expect(hudVariantStyle).toMatch(/\[data-variant="hud"\]\.hud::before\s*\{[^}]*background:\s*var\(--surface-navy\)/);
    expect(hudVariantStyle).toContain("var(--panel-edge-fade)");
    expect(hudVariantStyle).not.toContain("--hud-panel-face");
    expect(hudVariantStyle).not.toContain("--hud-panel-edge-fade");
  });

  it("hudのpaddingセレクタは.panelより詳細度で勝ち、import順に依存しない", () => {
    // .panel(style.module.css)は単純クラス(0,1,0)のため、hud側は属性+クラスの複合(0,2,0)で確実に上回る
    // .panel (style.module.css) is a plain class (0,1,0); pair an attribute selector with the class so hud always wins at (0,2,0)
    expect(hudVariantStyle).toMatch(/\[data-variant="hud"\]\.hud\s*\{[^}]*padding:\s*var\(--hud-panel-padding\)/);
  });

  it("既定面は肯定形のvariant判定で敷き、重なり順は全variant共通にする", () => {
    // 除外連鎖の食い違いで.bodyが面の裏へ沈む罠を、肯定形で構造的に潰す
    // Positive-form predicates remove the trap where a mismatched :not() chain sinks .body behind the face
    expect(style).toContain('.panel[data-variant="default"]::before');
    expect(style).toContain(".panel > *:not(.bottomDeco)");
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
