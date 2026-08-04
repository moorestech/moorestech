export const captureViewport = { width: 1280, height: 720 } as const;

export const expectedObjectives = {
  challengeJapanese: ["石を採掘する"],
  challengeMultiple: ["石を採掘する", "石器をクラフトする", "木を伐採して拠点へ運ぶ"],
  challengeLong: ["VeryLongUnbrokenChallengeObjectiveTextThatMustWrapInsideTheHudWithoutOverflowingAndStillRemainReadableAcrossEveryMenuScreenWithoutChangingTheChallengeHudLayout"],
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
  uiState: "GameScreen" | "PlayerInventory" | "SubInventory" | "ResearchTree" | "BuildMenu" | "ChallengeList" | "PauseMenu" | "PlaceBlock" | "DeleteBar";
  skit: "none" | "background" | "text";
  background: "world" | "bright" | "dark";
  companionScenario: "placement" | null;
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
  // 操作モードとの表示分離を検証する
  // Verify separation from operation-mode cues
  { name: "12-place-block-visible", scenario: "challengeJapanese", uiState: "PlaceBlock", skit: "none", background: "world", companionScenario: "placement" },
  { name: "13-delete-bar-visible", scenario: "challengeJapanese", uiState: "DeleteBar", skit: "none", background: "world", companionScenario: null },
  { name: "14-multiple-long-inventory-visible", scenario: "challengeMultipleLong", uiState: "PlayerInventory", skit: "none", background: "world", companionScenario: null },
  // 操作HUDを明暗背景で検証する
  // Verify operation HUDs on bright and dark backgrounds
  { name: "15-place-block-bright", scenario: "challengeJapanese", uiState: "PlaceBlock", skit: "none", background: "bright", companionScenario: "placement" },
  { name: "16-place-block-dark", scenario: "challengeJapanese", uiState: "PlaceBlock", skit: "none", background: "dark", companionScenario: "placement" },
  { name: "17-delete-bar-bright", scenario: "challengeJapanese", uiState: "DeleteBar", skit: "none", background: "bright", companionScenario: null },
  { name: "18-delete-bar-dark", scenario: "challengeJapanese", uiState: "DeleteBar", skit: "none", background: "dark", companionScenario: null },
  // メニュー固有面と常駐HUDを目視する
  // Visually verify the resident HUD against menu-specific surfaces
  { name: "19-sub-inventory-long", scenario: "challengeMultipleLong", uiState: "SubInventory", skit: "none", background: "world", companionScenario: null },
  { name: "20-research-long", scenario: "challengeMultipleLong", uiState: "ResearchTree", skit: "none", background: "world", companionScenario: null },
  { name: "21-build-menu-long", scenario: "challengeMultipleLong", uiState: "BuildMenu", skit: "none", background: "world", companionScenario: null },
  { name: "22-pause-menu-long", scenario: "challengeMultipleLong", uiState: "PauseMenu", skit: "none", background: "world", companionScenario: null },
];

export const captureImageNames = captureCases.map((captureCase) => `${captureCase.name}.png`);
