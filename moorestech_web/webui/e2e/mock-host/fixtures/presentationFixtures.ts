import type { ChallengeCurrentData, ChallengeTreeData, GameStateData, SkitPresentationData, TutorialPresentationData, WorldPinPresentationData } from "../../../src/bridge/contract/payloadTypes";

export const challengeTree = {
  categories: [{
    guid: "81000000-0000-4000-8000-000000000001",
    iconItemId: 1,
    nodes: [
      { guid: "82000000-0000-4000-8000-000000000001", iconItemId: 1, state: "completed", position: { x: 0, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: [] },
      { guid: "82000000-0000-4000-8000-000000000002", iconItemId: 2, state: "current", position: { x: 220, y: 0 }, scale: { x: 1, y: 1 }, prevGuids: ["82000000-0000-4000-8000-000000000001"] },
    ],
  }],
} satisfies ChallengeTreeData;
export const challengeCurrent = { challenges: [{ guid: "82000000-0000-4000-8000-000000000002", categoryGuid: "81000000-0000-4000-8000-000000000001" }] } satisfies ChallengeCurrentData;
export const challengeJapanese = {
  challenges: [{ guid: "82000000-0000-4000-8000-000000000003", categoryGuid: "81000000-0000-4000-8000-000000000001" }],
} satisfies ChallengeCurrentData;
export const challengeMultiple = {
  challenges: [
    { guid: "82000000-0000-4000-8000-000000000004", categoryGuid: "81000000-0000-4000-8000-000000000001" },
    { guid: "82000000-0000-4000-8000-000000000005", categoryGuid: "81000000-0000-4000-8000-000000000001" },
    { guid: "82000000-0000-4000-8000-000000000006", categoryGuid: "81000000-0000-4000-8000-000000000002" },
  ],
} satisfies ChallengeCurrentData;
export const challengeLong = {
  challenges: [{
    guid: "82000000-0000-4000-8000-000000000007",
    categoryGuid: "81000000-0000-4000-8000-000000000001",
  }],
} satisfies ChallengeCurrentData;
export const challengeMultipleLong = {
  challenges: [
    { guid: "82000000-0000-4000-8000-000000000008", categoryGuid: "81000000-0000-4000-8000-000000000001" },
    { guid: "82000000-0000-4000-8000-000000000009", categoryGuid: "81000000-0000-4000-8000-000000000002" },
    { guid: "82000000-0000-4000-8000-00000000000a", categoryGuid: "81000000-0000-4000-8000-000000000003" },
  ],
} satisfies ChallengeCurrentData;
export const gameState = { state: "InGame" } satisfies GameStateData;
export const tutorialPresentation = { revision: 0, sessions: [] } satisfies TutorialPresentationData;
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
