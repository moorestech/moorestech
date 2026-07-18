import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

describe("App architecture", () => {
  it("画面固有クロームとドメインactionを持たない", () => {
    const source = readFileSync(new URL("./App.tsx", import.meta.url), "utf8");
    expect(source).not.toContain("dispatchAction");
    expect(source).not.toContain("clearSelectedItem");
    expect(source).not.toContain("keyHints");
    expect(source).not.toContain("sortButton");
  });
});
