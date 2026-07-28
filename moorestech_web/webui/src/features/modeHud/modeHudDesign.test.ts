// 操作HUDのクラフト枠契約を固定する
// Locks operation HUDs to the craft-frame contract
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const placementHud = read("./PlacementModeHud.tsx");
const deleteHud = read("./DeleteModeHud.tsx");
const styles = read("./style.module.css");
const tokens = read("../../app/tokens.css");
const notificationStyles = read("../notification/style.module.css");
const networkSections = read("../blockInventory/details/NetworkSections.tsx");
const lackHighlightStyles = read("../blockInventory/details/LackHighlightText/style.module.css");

describe("operation mode HUD design whitelist", () => {
  it.each([
    ["placement", placementHud],
    ["delete", deleteHud],
  ])("%s HUDをMantineではなく共通クラフト枠で構成する", (_, source) => {
    expect(source).not.toContain("@mantine/core");
    expect(source).not.toMatch(/<(?:Paper|Stack|Title|Text)\b/);
    expect(source).toContain("GamePanel");
    expect(source).toContain('variant="craft"');
    expect(source).toContain("<section");
    expect(source).toContain("aria-labelledby={headingId}");
    expect(source).toContain("<h2");
    expect(source).toContain("<p");
    expect(source).toContain("<FadeRule");
  });

  it("機能CSSへ独自の面を作らず配置と文字階層だけを置く", () => {
    expect(styles).not.toMatch(/\b(?:background(?:-color|-image)?|border(?:-\w+)?|box-shadow)\s*:/);
    expect(styles).not.toMatch(/::(?:before|after)/);
    expect(styles).not.toMatch(/:\s*(?:white|red|#[0-9a-f]{3,8}|rgb\()/i);
    expect(styles).toContain("pointer-events: none");
    expect(styles).toContain("var(--text-muted)");
    expect(styles).toContain("var(--text-high-contrast)");
    expect(styles).toContain("var(--text-insufficient)");
  });

  it("操作モードHUDの視覚寸法を固定長トークンへ集約する", () => {
    expect(tokens).toContain("--operation-hud-left:");
    expect(tokens).toContain("--operation-hud-top:");
    expect(tokens).toContain("--operation-hud-width:");
    expect(tokens).toContain("--operation-hud-label-font-size:");
    expect(tokens).toContain("--operation-hud-detail-font-size:");
    expect(tokens).not.toContain("--operation-hud-text-shadow:");
  });

  it("警告色のコントラスト改善を既存consumerへ共有する", () => {
    expect(tokens).toContain("--text-insufficient: #ff7878");
    expect(notificationStyles).toContain("var(--text-insufficient)");
    expect(networkSections).toContain("var(--text-insufficient)");
    expect(lackHighlightStyles).toContain("var(--text-insufficient)");
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
