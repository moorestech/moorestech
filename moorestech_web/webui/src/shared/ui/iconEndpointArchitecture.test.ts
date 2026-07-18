import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

describe("icon endpoint architecture", () => {
  it.each(["./ItemIcon.tsx", "./BlockIcon.tsx"])("%s にHTTPパスを直書きしない", (path) => {
    const source = readFileSync(new URL(path, import.meta.url), "utf8");
    expect(source).not.toContain("/api/");
  });
});
