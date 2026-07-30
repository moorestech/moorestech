import type { ChallengeCurrentData, ChallengeTreeData, GameStateData, SkitPresentationData, TutorialPresentationData, WorldPinPresentationData } from "../../../src/bridge/contract/payloadTypes";

export const challengeTree = {
  categories: [{
    guid: "cat-1",
    iconItemId: 1,
    nodes: [
      { guid: "ch-1", iconItemId: 1, state: "completed", position: { x: 0, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: [] },
      { guid: "ch-2", iconItemId: 2, state: "current", position: { x: 220, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: ["ch-1"] },
    ],
  }],
} satisfies ChallengeTreeData;
export const challengeCurrent = { challenges: [{ guid: "ch-2", categoryGuid: "cat-1" }] } satisfies ChallengeCurrentData;
export const challengeJapanese = {
  challenges: [{ guid: "ch-jp", categoryGuid: "cat-1" }],
} satisfies ChallengeCurrentData;
export const challengeMultiple = {
  challenges: [
    { guid: "ch-a", categoryGuid: "cat-1" },
    { guid: "ch-b", categoryGuid: "cat-1" },
    { guid: "ch-c", categoryGuid: "cat-2" },
  ],
} satisfies ChallengeCurrentData;
export const challengeLong = {
  challenges: [{
    guid: "ch-long",
    categoryGuid: "cat-1",
  }],
} satisfies ChallengeCurrentData;
export const challengeMultipleLong = {
  challenges: [
    { guid: "ch-ml-a", categoryGuid: "cat-1" },
    { guid: "ch-ml-b", categoryGuid: "cat-2" },
    { guid: "ch-ml-c", categoryGuid: "cat-3" },
  ],
} satisfies ChallengeCurrentData;
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
