// 通知の背面層固定（ADR 0017）
// Notification stays in behind-stage layer (ADR 0017)
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const style = readFileSync(new URL("./style.module.css", import.meta.url), "utf8");
const app = readFileSync(new URL("../../app/App.tsx", import.meta.url), "utf8");

describe("notification layering", () => {
  it("通知ホストは背面層のトークンを使い、最前面のトースト層を使わない", () => {
    expect(style).toContain("z-index: var(--z-viewport-behind-stage)");
    expect(style).not.toContain("var(--z-portal-toast)");
  });

  it("通知はPortalの外、stageより前のDOM位置に描かれる", () => {
    // Portal内はbody直下の兄弟になり、zをどう下げても.viewportより前に描かれる
    // Inside the portal it becomes a body-level sibling and paints ahead of .viewport at any z
    const hostIndex = app.indexOf("<NotificationHost />");
    const portalIndex = app.indexOf("<Portal>");
    // stageの目印はclassNameでなくtestId。修飾クラス合成でclassName記述が変わっても位置検査は生き残る
    // Anchor the stage by its testId, not its className, so composing modifier classes cannot break this position check
    const stageIndex = app.indexOf('data-testid="app-stage"');
    expect(hostIndex).toBeGreaterThan(-1);
    expect(hostIndex).toBeLessThan(stageIndex);
    expect(hostIndex).toBeLessThan(portalIndex);
  });

  it("通知は実画面へ固定され、stage拡縮に追従しない", () => {
    expect(style).toContain("position: fixed");
    expect(style).toContain("top: 50%");
    expect(style).toContain("left: 1rem");
  });
});
