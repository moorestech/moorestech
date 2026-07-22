import type { GameStateData, SkitPresentationData, TutorialPresentationData, WorldPinPresentationData } from "../../../src/bridge/contract/payloadTypes";

export const challengeTree = {
  categories: [{
    guid: "cat-1",
    name: "Basics",
    iconItemId: 1,
    nodes: [
      { guid: "ch-1", title: "First Craft", summary: "craft something", iconItemId: 1, state: "completed", position: { x: 0, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: [] },
      { guid: "ch-2", title: "Second Step", summary: "keep going", iconItemId: 2, state: "current", position: { x: 220, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: ["ch-1"] },
    ],
  }],
};
export const challengeCurrent = { challenges: [{ guid: "ch-2", title: "Second Step", categoryGuid: "cat-1" }] };
export const gameState = { state: "InGame" } satisfies GameStateData;
export const tutorialPresentation = {
  tutorialSessionId: "", revision: 0, challengeId: "", highlights: [],
} satisfies TutorialPresentationData;
export const worldPins = { revision: 0, pins: [] } satisfies WorldPinPresentationData;
export const skitPresentation = {
  sessionId: "", sceneRevision: 0,
  presentationState: {
    mode: "none", speakerName: "", body: "", choices: [], textAreaVisible: false,
    transitionVisible: false, autoEnabled: false, skipActive: false, uiHidden: false,
    textReveal: { mode: "instant", intervalMs: 0 },
  },
  allowedIntents: [],
} satisfies SkitPresentationData;
