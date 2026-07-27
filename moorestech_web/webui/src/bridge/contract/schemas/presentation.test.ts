import { describe, expect, it } from "vitest";
import {
  GameStateDataSchema, SkitPresentationDataSchema,
  TutorialHighlightSchema, TutorialPresentationDataSchema,
} from "./presentation";

describe("Phase C4 presentation contracts", () => {
  it.each(["outline", "callout"] as const)("accepts the %s tutorial highlight kind", (kind) => {
    expect(TutorialHighlightSchema.safeParse({
      highlightId: "highlight-1", anchorId: "game.crosshair", kind,
      message: "", paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(true);
  });

  it("rejects the removed spotlight tutorial highlight kind", () => {
    expect(TutorialHighlightSchema.safeParse({
      highlightId: "highlight-1", anchorId: "game.crosshair", kind: "spotlight",
      message: "", paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(false);
  });

  it("accepts the three idle snapshots", () => {
    expect(GameStateDataSchema.parse({ state: "InGame" })).toEqual({ state: "InGame" });
    expect(TutorialPresentationDataSchema.parse({
      tutorialSessionId: "", revision: 0, challengeId: "", highlights: [],
    }).highlights).toEqual([]);
    expect(SkitPresentationDataSchema.parse({
      sessionId: "", sceneRevision: 0, presentationState: {
        mode: "none", speakerName: "", body: "", choices: [], textAreaVisible: false,
        transitionVisible: false, autoEnabled: false, skipActive: false, uiHidden: false,
        textReveal: { mode: "instant", intervalMs: 0 },
      }, allowedIntents: [],
    }).presentationState.mode).toBe("none");
  });
});
