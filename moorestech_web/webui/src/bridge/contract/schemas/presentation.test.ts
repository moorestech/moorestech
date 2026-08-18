import { describe, expect, it } from "vitest";
import {
  GameStateDataSchema, SkitPresentationDataSchema,
  TutorialDragGuideSchema, TutorialHighlightSchema, TutorialPresentationDataSchema,
} from "./presentation";

describe("Phase C4 presentation contracts", () => {
  it("accepts the outline tutorial highlight kind", () => {
    expect(TutorialHighlightSchema.safeParse({
      highlightId: "highlight-1", anchorId: "game.crosshair", kind: "outline",
      paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(true);
  });

  it.each(["spotlight", "callout"] as const)("rejects the removed %s tutorial highlight kind", (kind) => {
    expect(TutorialHighlightSchema.safeParse({
      highlightId: "highlight-1", anchorId: "game.crosshair", kind,
      paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(false);
  });

  it("rejects a host-resolved highlight message", () => {
    expect(TutorialHighlightSchema.safeParse({
      highlightId: "highlight-1", anchorId: "game.crosshair", kind: "outline",
      message: "Craft", paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(false);
  });

  it("accepts the three idle snapshots", () => {
    expect(GameStateDataSchema.parse({ state: "InGame" })).toEqual({ state: "InGame" });
    expect(TutorialPresentationDataSchema.parse({
      tutorialSessionId: "", revision: 0, challengeId: "", highlights: [], dragGuides: [],
    }).highlights).toEqual([]);
    expect(SkitPresentationDataSchema.parse({
      sessionId: "", sceneRevision: 0, presentationState: {
        mode: "none", speakerName: "", body: "", choices: [], textAreaVisible: false,
        transitionVisible: false, autoEnabled: false, skipActive: false, uiHidden: false,
        textReveal: { mode: "instant", intervalMs: 0 },
      }, allowedIntents: [],
    }).presentationState.mode).toBe("none");
  });

  it("accepts a drag guide from a hotbar anchor to a build menu entry", () => {
    expect(TutorialDragGuideSchema.safeParse({
      guideId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "build-menu.entry-block-abc",
    }).success).toBe(true);
  });

  it("carries dragGuides on the tutorial presentation snapshot", () => {
    const parsed = TutorialPresentationDataSchema.parse({
      tutorialSessionId: "session-1", revision: 1, challengeId: "challenge-1", highlights: [],
      dragGuides: [{ guideId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "challenge.current-hud" }],
    });
    expect(parsed.dragGuides).toEqual([
      { guideId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "challenge.current-hud" },
    ]);
  });
});
