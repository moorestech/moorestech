import { NestedPauseSubStateNames, UiStateNames } from "@/bridge";

// ui_state.current の state 名 → Web が描画する画面。App.tsx ルーティングの単一の正
// Maps ui_state.current's state name to the web screen; single source for App.tsx routing
export type UiScreen = "none" | "playerInventory" | "subInventory" | "researchTree" | "buildMenu" | "challengeList" | "pauseMenu" | "trainHud" | "trainPause" | "skitPause";

// 画面ごとの性質を1表に畳む。述語は表の参照だけを行い、画面名の羅列を持たない（前例 researchLogic.ts の STATE_TRAITS）
// Screen traits collapse into one table; the predicates only read it and never enumerate screen names (precedent: STATE_TRAITS in researchLogic.ts)
type ScreenTraits = {
  grab: boolean;
  pauseMenu: boolean;
  skitInput: boolean;
  backdrop: boolean;
  alwaysOnHud: boolean;
  trainHud: boolean;
};

const SCREEN_TRAITS = {
  none: { grab: false, pauseMenu: false, skitInput: true, backdrop: false, alwaysOnHud: true, trainHud: false },
  playerInventory: { grab: true, pauseMenu: false, skitInput: true, backdrop: true, alwaysOnHud: true, trainHud: false },
  subInventory: { grab: true, pauseMenu: false, skitInput: true, backdrop: true, alwaysOnHud: true, trainHud: false },
  researchTree: { grab: true, pauseMenu: false, skitInput: true, backdrop: true, alwaysOnHud: false, trainHud: false },
  buildMenu: { grab: false, pauseMenu: false, skitInput: true, backdrop: true, alwaysOnHud: true, trainHud: false },
  challengeList: { grab: false, pauseMenu: false, skitInput: true, backdrop: true, alwaysOnHud: true, trainHud: false },
  pauseMenu: { grab: false, pauseMenu: true, skitInput: true, backdrop: true, alwaysOnHud: true, trainHud: false },
  trainHud: { grab: false, pauseMenu: false, skitInput: true, backdrop: false, alwaysOnHud: true, trainHud: true },
  trainPause: { grab: false, pauseMenu: true, skitInput: true, backdrop: true, alwaysOnHud: true, trainHud: true },
  // スキットのポーズ中だけ表示を続けたまま入力口を閉じる
  // Only the skit's pause keeps drawing while every input path closes
  skitPause: { grab: false, pauseMenu: true, skitInput: false, backdrop: true, alwaysOnHud: true, trainHud: false },
} satisfies Record<UiScreen, ScreenTraits>;

export function screenForUiState(state: string | null, subState?: string): UiScreen {
  if (state === UiStateNames.playerInventory) return "playerInventory";
  if (state === UiStateNames.subInventory) return "subInventory";
  if (state === UiStateNames.researchTree) return "researchTree";
  if (state === UiStateNames.buildMenu) return "buildMenu";
  if (state === UiStateNames.challengeList) return "challengeList";
  if (state === UiStateNames.pauseMenu) return "pauseMenu";
  if (state === UiStateNames.trainHud) return isNestedPauseSubState(subState) ? "trainPause" : "trainHud";
  // スキット本体はskitPresentationトピックが描くので、Storyはポーズ入れ子だけを画面に昇格させる
  // The skit itself is drawn from the skitPresentation topic, so Story only promotes its nested pause to a screen
  if (state === UiStateNames.story) return isNestedPauseSubState(subState) ? "skitPause" : "none";
  // GameScreen・未対応state・未受信はパネル無し（前方互換: 未知state名も安全側に倒す)
  // GameScreen, unsupported states and pre-snapshot are panel-less (forward-compat: unknown names fail safe)
  return "none";
}

// 入れ子ポーズ画面のサブstate語彙は列車HUDとスキットで共通（C#のNestedPauseSubStateEnum）
// Nested-pause screens share one sub-state vocabulary between the train HUD and the skit (C#'s NestedPauseSubStateEnum)
function isNestedPauseSubState(subState: string | undefined): boolean {
  return subState === NestedPauseSubStateNames.pauseMenuScreen;
}

// マスタ由来のuiState名がUnityのUIStateEnum語彙に載っているか。載らない値は照合せず捨てる
// Whether a master-authored uiState name exists in Unity's UIStateEnum vocabulary; unlisted values never match
export function isKnownUiStateName(name: string): boolean {
  return (Object.values(UiStateNames) as string[]).includes(name);
}

// ホットバー選択を消費するのは GameScreen と PlaceBlock だけ（C#側 HotbarSelectActionHandler と同一条件）
// Only GameScreen and PlaceBlock consume a hotbar selection; mirrors the C# HotbarSelectActionHandler gate
export function uiStateAcceptsHotbarSelect(state: string | null): boolean {
  return state === UiStateNames.gameScreen || state === UiStateNames.placeBlock;
}

// grab は掴んだ絵が見える画面でしか成立しない。クリック可否と GrabOverlay 描画の単一の正
// A grab only holds where the held item is visible; single source for clickability and GrabOverlay
export function screenAllowsGrab(screen: UiScreen): boolean {
  return SCREEN_TRAITS[screen].grab;
}

// ポーズメニューを出す画面族
// The family of screens that show the pause menu
export function screenShowsPauseMenu(screen: UiScreen): boolean {
  return SCREEN_TRAITS[screen].pauseMenu;
}

// スキット表示層が入力を受け付ける画面族。ポーズ中は表示を続けたまま入力口だけ閉じる
// The family of screens where the skit layer takes input; during pause it keeps drawing but closes every input path
export function screenAllowsSkitInput(screen: UiScreen): boolean {
  return SCREEN_TRAITS[screen].skitInput;
}

// 背景ディムを出す画面族（trainHud除く）
// The family of screens that show the dim backdrop (excluding trainHud)
export function screenShowsBackdrop(screen: UiScreen): boolean {
  return SCREEN_TRAITS[screen].backdrop;
}

// 常時表示族は研究画面でだけ引っ込む
// The always-on family withdraws only on the research screen
// 常駐チャレンジHUDと採掘進捗バーはこの族に含まず、研究画面でも出したままにする
// The resident challenge HUD and the mining progress bar are not in this family and stay visible there
export function screenShowsAlwaysOnHud(screen: UiScreen): boolean {
  return SCREEN_TRAITS[screen].alwaysOnHud;
}

// 乗車HUDを出す画面族。ポーズを重ねても乗車表示は残す
// The family of screens that show the riding HUD; it stays put even under the pause menu
export function screenShowsTrainHud(screen: UiScreen): boolean {
  return SCREEN_TRAITS[screen].trainHud;
}
