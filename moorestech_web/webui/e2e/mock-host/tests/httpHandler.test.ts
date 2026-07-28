import type { AddressInfo } from "node:net";
import { describe, expect, it, onTestFinished } from "vitest";
import { blockIconUrl, itemIconUrl } from "../../../src/bridge/transport/httpEndpoints";
import { createMockHttpServer, injectDemoBackground } from "../httpHandler";

describe("injectDemoBackground", () => {
  const html = "<html><body><div id=\"root\"></div></body></html>";

  it("injects the orange background into demo HTML", () => {
    const result = injectDemoBackground(html, true);

    expect(result).toContain('id="__worldbg"');
    expect(result).toContain("url('/mock-orange-gradient.png')");
  });

  it("injects after a body tag with attributes", () => {
    const result = injectDemoBackground('<html><body class="mock"><div id="root"></div></body></html>', true);

    expect(result).toContain('<body class="mock"><div id="__worldbg"');
  });

  it("leaves non-demo HTML unchanged", () => {
    expect(injectDemoBackground(html, false)).toBe(html);
  });
});

// 本番が使う2つのアイコン経路が SPA フォールバック(200 HTML)に落ちず、非DEMOでは揃って404になること
// Both production icon paths must 404 outside DEMO instead of falling through to the SPA fallback (200 HTML)
describe("icon endpoints", () => {
  it("アイテムとブロックのアイコンURLが同じ404応答になる", async () => {
    const server = createMockHttpServer();
    // アサート失敗時もサーバを確実に閉じる（close 行未到達によるリーク防止）
    // Always close the server even when the assertion fails (prevents a leak from the unreached close line)
    onTestFinished(() => new Promise<void>((resolve) => server.close(() => resolve())));
    await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
    const { port } = server.address() as AddressInfo;

    const responses = await Promise.all(
      [itemIconUrl(12), blockIconUrl(12)].map((path) => fetch(`http://127.0.0.1:${port}${path}`)),
    );

    expect(responses.map((res) => res.status)).toEqual([404, 404]);
  });
});
