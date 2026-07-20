import { describe, expect, it } from "vitest";
import { GameStateDataSchema, SkitPresentationDataSchema, TutorialPresentationDataSchema } from "./presentation";

describe("Phase C4 presentation contracts", () => {
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
