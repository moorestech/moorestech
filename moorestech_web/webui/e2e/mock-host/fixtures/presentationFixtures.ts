import type { ChallengeCurrentData, ChallengeTreeData, GameStateData, SkitPresentationData, TutorialPresentationData, WorldPinPresentationData } from "../../../src/bridge/contract/payloadTypes";

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
} satisfies ChallengeTreeData;
export const challengeCurrent = { challenges: [{ guid: "ch-2", title: "Second Step", categoryGuid: "cat-1" }] } satisfies ChallengeCurrentData;
export const challengeJapanese = {
  challenges: [{ guid: "ch-jp", title: "石を採掘する", categoryGuid: "cat-1" }],
};
export const challengeMultiple = {
  challenges: [
    { guid: "ch-a", title: "石を採掘する", categoryGuid: "cat-1" },
    { guid: "ch-b", title: "石器をクラフトする", categoryGuid: "cat-1" },
    { guid: "ch-c", title: "木を伐採して拠点へ運ぶ", categoryGuid: "cat-2" },
  ],
};
export const challengeLong = {
  challenges: [{
    guid: "ch-long",
    title: "VeryLongUnbrokenChallengeObjectiveTextThatMustWrapInsideTheHudWithoutOverflowingAndStillRemainReadableAcrossEveryMenuScreenWithoutChangingTheChallengeHudLayout",
    categoryGuid: "cat-1",
  }],
};
export const challengeMultipleLong = {
  challenges: [
    { guid: "ch-ml-a", title: "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する", categoryGuid: "cat-1" },
    { guid: "ch-ml-b", title: "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する", categoryGuid: "cat-2" },
    { guid: "ch-ml-c", title: "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud", categoryGuid: "cat-3" },
  ],
};
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
