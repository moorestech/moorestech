// 配置枠と削除帯の契約を固定する
// Lock placement-frame and deletion-band contracts
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const placementHud = read("./PlacementModeHud.tsx");
const deleteWarning = read("./DeleteModeWarningBands.tsx");
const styles = read("./style.module.css");
const tokens = read("../../app/tokens.css");
const notificationStyles = read("../notification/style.module.css");
const networkSections = read("../blockInventory/details/NetworkSections.tsx");
const lackHighlightStyles = read("../blockInventory/details/LackHighlightText/style.module.css");

describe("operation mode HUD design whitelist", () => {
  it("配置HUDをMantineではなく共通クラフト枠で構成する", () => {
    expect(placementHud).not.toContain("@mantine/core");
    expect(placementHud).not.toMatch(/<(?:Paper|Stack|Title|Text)\b/);
    expect(placementHud).toContain("GamePanel");
    expect(placementHud).toContain('variant="craft"');
    expect(placementHud).toContain("<FadeRule");
  });

  it("削除表示を説明パネルではなく上下の警告帯で構成する", () => {
    expect(deleteWarning).not.toContain("GamePanel");
    expect(deleteWarning).not.toContain("Topics.deleteMode");
    expect(deleteWarning).toContain('role="status"');
    expect(deleteWarning.match(/data-testid="delete-mode-warning-band"/g)).toHaveLength(2);
  });

  it("機能CSSの色と寸法をトークン経由に限定する", () => {
    expect(styles).not.toMatch(/::(?:before|after)/);
    expect(styles).not.toMatch(/:\s*(?:white|red|#[0-9a-f]{3,8}|rgb\()/i);
    expect(styles).toContain("pointer-events: none");
    expect(styles).toContain("var(--text-muted)");
    expect(styles).toContain("var(--text-high-contrast)");
    expect(styles).toContain("var(--text-insufficient)");
    expect(styles).toContain("repeating-linear-gradient");
    expect(styles).toContain("var(--delete-mode-warning-yellow)");
    expect(styles).toContain("var(--delete-mode-warning-black)");
  });

  it("操作モードHUDの視覚寸法を固定長トークンへ集約する", () => {
    expect(tokens).toContain("--operation-hud-right:");
    expect(tokens).toContain("--operation-hud-top:");
    expect(tokens).toContain("--operation-hud-width:");
    expect(tokens).toContain("--operation-hud-label-font-size:");
    expect(tokens).toContain("--operation-hud-detail-font-size:");
    expect(tokens).toContain("--delete-mode-warning-band-height:");
    expect(tokens).toContain("--delete-mode-warning-stripe-size:");
    expect(tokens).toContain("--delete-mode-warning-stripe-angle:");
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
