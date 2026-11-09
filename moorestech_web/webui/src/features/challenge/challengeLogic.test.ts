import { describe, expect, it } from "vitest";
import type { ChallengeNodeData } from "@/bridge";
import { findChallengeInitialFocusNode } from "./challengeLogic";

const node = (guid: string, state: ChallengeNodeData["state"]): ChallengeNodeData => ({ guid, iconItemId: 1, state, position: { x: 0, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: [] });

describe("findChallengeInitialFocusNode", () => {
  it("進行中(current)のノードを返す", () => {
    expect(findChallengeInitialFocusNode([node("a", "completed"), node("b", "current"), node("c", "locked")])?.guid).toBe("b");
  });

  it("進行中が無ければ寄せない", () => {
    expect(findChallengeInitialFocusNode([node("a", "completed"), node("c", "locked")])).toBeNull();
    expect(findChallengeInitialFocusNode([])).toBeNull();
  });
});
