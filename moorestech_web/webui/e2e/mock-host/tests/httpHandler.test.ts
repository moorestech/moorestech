import { describe, expect, it } from "vitest";
import { injectDemoBackground } from "../httpHandler";

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
