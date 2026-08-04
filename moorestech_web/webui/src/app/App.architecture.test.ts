import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

describe("App architecture", () => {
  it("辞書ロード失敗オーバーレイは操作を取り戻すリロード手段を持つ", () => {
    // 全面Overlayはポインタを捕捉し自動復帰もしないため、解除手段の欠落は恒久ロックになる
    // The full-screen overlay captures pointers and never self-heals, so a missing escape hatch locks the UI forever
    const source = readFileSync(new URL("./App.tsx", import.meta.url), "utf8");
    const overlay = source.slice(source.indexOf("dictionary-error-overlay"));

    expect(overlay).toContain("location.reload()");
    expect(overlay).toContain("DictionaryIndependentText.reload");
  });

  it("画面固有クロームとドメインactionを持たない", () => {
    const source = readFileSync(new URL("./App.tsx", import.meta.url), "utf8");
    expect(source).not.toContain("dispatchAction");
    expect(source).not.toContain("clearSelectedItem");
    expect(source).not.toContain("keyHints");
    expect(source).not.toContain("sortButton");
  });
});
