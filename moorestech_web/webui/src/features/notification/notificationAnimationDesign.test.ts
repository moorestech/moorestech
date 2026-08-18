// 生存尺を単一の正にする検証
// Verifies a single lifetime source drives the CSS
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const host = read("./NotificationHost.tsx");
const store = read("./notificationStore.ts");
const style = read("./style.module.css");
const tokens = read("../../app/tokens.css");

describe("notification enter/exit animation", () => {
  it("生存尺はstoreが唯一の正で、CSSへ尺を直書きしない", () => {
    expect(store).toContain("export const NOTIFICATION_DISPLAY_MS = 7000");
    expect(host).toContain('"--notification-lifetime": `${NOTIFICATION_DISPLAY_MS}ms`');
    expect(style).not.toMatch(/7000ms|7s\b/);
  });

  it("入場と退場の2本を持ち、退場は生存尺から逆算した遅延で始まる", () => {
    expect(style).toContain("animation:");
    expect(style).toContain("notificationEnter var(--notification-enter-duration)");
    expect(style).toContain("calc(var(--notification-lifetime) - var(--notification-exit-duration))");
  });

  it("退場のfill-modeはforwardsで入場を巻き戻さない", () => {
    // bothにすると遅延中に退場のfrom状態が前方適用され、入場アニメが消える
    // Using both would back-fill the exit's from state during the delay and erase the enter animation
    expect(style).toMatch(/notificationExit[^;]*forwards/);
    expect(style).not.toMatch(/notificationExit[^;]*\bboth\b/);
  });

  it("移動量と尺はトークンで管理する", () => {
    expect(tokens).toContain("--notification-enter-duration: 160ms");
    expect(tokens).toContain("--notification-exit-duration: 200ms");
    expect(tokens).toContain("--notification-shift: 12px");
  });

  it("除去は退場アニメの終了が主で、タイマーは尺より後ろの保険に留める", () => {
    // 削除(JS)と退場(CSS)が別時計だと、行が消えた後も面が残る/面が消える前に消える
    // If the delete (JS) and the exit (CSS) run on separate clocks, the row and its face stop agreeing
    expect(host).toContain("event.animationName === styles.notificationExit");
    expect(store).toContain("NOTIFICATION_REMOVAL_FALLBACK_MS = NOTIFICATION_DISPLAY_MS + 1000");
    expect(store).toContain("NOTIFICATION_REMOVAL_FALLBACK_MS)");
  });

  it("生存尺はホストでなく各行へ渡し、面と同じ要素の上で解決させる", () => {
    const rowMarkup = host.slice(host.indexOf("styles.notification}"));
    expect(rowMarkup).toContain("style={lifetimeStyle}");
    expect(host.slice(0, host.indexOf("styles.notification}"))).not.toContain("style={lifetimeStyle}");
  });

  it("同時表示数の上限を持たない", () => {
    expect(store).not.toMatch(/slice\(|MAX_|limit/);
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
