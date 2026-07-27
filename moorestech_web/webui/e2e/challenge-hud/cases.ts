export const captureViewport = { width: 1280, height: 720 } as const;

export const expectedObjectives = {
  challengeJapanese: ["石を採掘する"],
  challengeMultiple: ["石を採掘する", "石器をクラフトする", "木を伐採して拠点へ運ぶ"],
  challengeLong: ["VeryLongUnbrokenChallengeObjectiveTextThatMustWrapInsideTheHudWithoutOverflowing"],
  challengeMultipleLong: [
    "地下深くにある非常に長い名前の鉱床を見つけて必要な石を採掘する",
    "遠方の森林から建築に必要な木材を伐採して拠点まで運搬する",
    "VeryLongUnbrokenSecondaryObjectiveTextThatMustAlsoWrapInsideTheHud",
  ],
  challengeCompleted: [],
} as const;

export type CaptureCase = {
  name: string;
  scenario: keyof typeof expectedObjectives;
  uiState: "GameScreen" | "PlayerInventory" | "ChallengeList" | "PlaceBlock" | "DeleteBar";
  skit: "none" | "background" | "text";
  background: "world" | "bright" | "dark";
  companionScenario: "placement" | "delete" | null;
};

// UI衝突ケースを固定順で網羅する
// Cover UI collision cases in a fixed order
export const captureCases: CaptureCase[] = [
  { name: "01-single-world", scenario: "challengeJapanese", uiState: "GameScreen", skit: "none", background: "world", companionScenario: null },
  { name: "02-single-bright", scenario: "challengeJapanese", uiState: "GameScreen", skit: "none", background: "bright", companionScenario: null },
  { name: "03-single-dark", scenario: "challengeJapanese", uiState: "GameScreen", skit: "none", background: "dark", companionScenario: null },
  { name: "04-multiple", scenario: "challengeMultiple", uiState: "GameScreen", skit: "none", background: "world", companionScenario: null },
  { name: "05-unbroken-long", scenario: "challengeLong", uiState: "GameScreen", skit: "none", background: "world", companionScenario: null },
  { name: "06-multiple-long", scenario: "challengeMultipleLong", uiState: "GameScreen", skit: "none", background: "world", companionScenario: null },
  // 画面・空・スキットの表示責務を検証する
  // Verify display ownership for screens, empty state, and skits
  { name: "07-inventory", scenario: "challengeJapanese", uiState: "PlayerInventory", skit: "none", background: "world", companionScenario: null },
  { name: "08-challenge-list", scenario: "challengeJapanese", uiState: "ChallengeList", skit: "none", background: "world", companionScenario: null },
  { name: "09-empty", scenario: "challengeCompleted", uiState: "GameScreen", skit: "none", background: "world", companionScenario: null },
  { name: "10-background-skit", scenario: "challengeJapanese", uiState: "GameScreen", skit: "background", background: "world", companionScenario: null },
  { name: "11-blocking-hidden", scenario: "challengeJapanese", uiState: "GameScreen", skit: "text", background: "world", companionScenario: null },
  // 操作HUD・モーダルの衝突を検証する
  // Verify operation-HUD and modal collisions
  { name: "12-place-block-hidden", scenario: "challengeJapanese", uiState: "PlaceBlock", skit: "none", background: "world", companionScenario: "placement" },
  { name: "13-delete-bar-hidden", scenario: "challengeJapanese", uiState: "DeleteBar", skit: "none", background: "world", companionScenario: "delete" },
  { name: "14-multiple-long-inventory-hidden", scenario: "challengeMultipleLong", uiState: "PlayerInventory", skit: "none", background: "world", companionScenario: null },
];

export const captureImageNames = captureCases.map((captureCase) => `${captureCase.name}.png`);
