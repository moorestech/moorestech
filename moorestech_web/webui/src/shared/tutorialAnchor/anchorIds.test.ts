import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { TutorialAnchorIds, TutorialAnchorDynamicPrefixes } from "./anchorIds";

// Unity側TutorialAnchorIdMapperの照合テストと同一フィクスチャを参照し、乖離を検知する
// Shared with Unity's TutorialAnchorIdMapper contract test to catch drift between the two sides
const fixturePath = fileURLToPath(
  new URL(
    "../../../../../moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json",
    import.meta.url,
  ),
);

function loadFixture(): { staticIds: string[]; dynamicPrefixes: Record<string, string> } {
  return JSON.parse(readFileSync(fixturePath, "utf8"));
}

describe("tutorial anchor IDs (shared fixture with Unity)", () => {
  it("static IDs match the shared fixture exactly", () => {
    const fixture = loadFixture();
    expect(Object.values(TutorialAnchorIds).sort()).toEqual([...fixture.staticIds].sort());
  });

  it("dynamic ID prefixes match the shared fixture exactly", () => {
    const fixture = loadFixture();
    expect(TutorialAnchorDynamicPrefixes).toEqual(fixture.dynamicPrefixes);
  });
});
