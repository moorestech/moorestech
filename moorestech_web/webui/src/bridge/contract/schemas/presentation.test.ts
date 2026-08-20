import { describe, expect, it } from "vitest";
import {
  GameStateDataSchema, SkitPresentationDataSchema,
  TutorialDragGuideSchema, TutorialHighlightSchema, TutorialOverlayElementSchema,
  TutorialPresentationDataSchema,
} from "./presentation";

describe("Phase C4 presentation contracts", () => {
  it("accepts the outline tutorial highlight kind", () => {
    expect(TutorialHighlightSchema.safeParse({
      kind: "outline", elementId: "highlight-1", anchorId: "game.crosshair",
      paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(true);
  });

  it.each(["spotlight", "callout"] as const)("rejects the removed %s tutorial highlight kind", (kind) => {
    expect(TutorialHighlightSchema.safeParse({
      kind, elementId: "highlight-1", anchorId: "game.crosshair",
      paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(false);
  });

  it("rejects a host-resolved highlight message", () => {
    expect(TutorialHighlightSchema.safeParse({
      kind: "outline", elementId: "highlight-1", anchorId: "game.crosshair",
      message: "Craft", paddingPx: 8, blocksPointerInput: false,
    }).success).toBe(false);
  });

  // kindを判別子にした単一列で受け、種別ごとの並列配列に戻さない
  // Accept one kind-discriminated list instead of reverting to per-kind parallel arrays
  it("discriminates overlay elements by kind within one list", () => {
    const parsed = TutorialOverlayElementSchema.array().parse([
      { kind: "outline", elementId: "highlight-1", anchorId: "game.crosshair", paddingPx: 8, blocksPointerInput: false },
      { kind: "dragGuide", elementId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "challenge.current-hud" },
    ]);
    expect(parsed.map((element) => element.kind)).toEqual(["outline", "dragGuide"]);
  });

  it("accepts the three idle snapshots", () => {
    expect(GameStateDataSchema.parse({ state: "InGame" })).toEqual({ state: "InGame" });
    expect(TutorialPresentationDataSchema.parse({ revision: 0, sessions: [] }).sessions).toEqual([]);
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
      kind: "dragGuide", elementId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "build-menu.entry-block-abc",
    }).success).toBe(true);
  });

  it("rejects a drag guide with an unknown extra key", () => {
    expect(TutorialDragGuideSchema.safeParse({
      kind: "dragGuide", elementId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "build-menu.entry-block-abc",
      message: "Drag here",
    }).success).toBe(false);
  });

  // 同時currentの複数challengeがsessionとして並存する
  // Simultaneously current challenges coexist as separate sessions
  it("carries one session per challenge on the tutorial presentation snapshot", () => {
    const parsed = TutorialPresentationDataSchema.parse({
      revision: 1,
      sessions: [
        { tutorialSessionId: "session-1", challengeId: "challenge-1", elements: [
          { kind: "dragGuide", elementId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "challenge.current-hud" },
        ] },
        { tutorialSessionId: "session-2", challengeId: "challenge-2", elements: [] },
      ],
    });
    expect(parsed.sessions.map((session) => session.challengeId)).toEqual(["challenge-1", "challenge-2"]);
    expect(parsed.sessions[0].elements[0]).toEqual(
      { kind: "dragGuide", elementId: "guide-1", fromAnchorId: "hotbar.hud", toAnchorId: "challenge.current-hud" },
    );
  });
});
