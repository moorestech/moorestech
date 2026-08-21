import { UiStateNames } from "@/bridge";

// ui_state.current の state 名 → Web が描画する画面。App.tsx ルーティングの単一の正
// Maps ui_state.current's state name to the web screen; single source for App.tsx routing
export type UiScreen = "none" | "playerInventory" | "subInventory" | "researchTree" | "buildMenu" | "challengeList" | "pauseMenu" | "trainHud" | "trainPause";

export function screenForUiState(state: string | null, subState?: string): UiScreen {
  if (state === UiStateNames.playerInventory) return "playerInventory";
  if (state === UiStateNames.subInventory) return "subInventory";
  if (state === UiStateNames.researchTree) return "researchTree";
  if (state === UiStateNames.buildMenu) return "buildMenu";
  if (state === UiStateNames.challengeList) return "challengeList";
  if (state === UiStateNames.pauseMenu) return "pauseMenu";
  if (state === UiStateNames.trainHud) return subState === "PauseMenuScreen" ? "trainPause" : "trainHud";
  // GameScreen・未対応state・未受信はパネル無し（前方互換: 未知state名も安全側に倒す)
  // GameScreen, unsupported states and pre-snapshot are panel-less (forward-compat: unknown names fail safe)
  return "none";
}

// ホットバー選択を消費するのは GameScreen と PlaceBlock だけ（C#側 HotbarSelectActionHandler と同一条件）
// Only GameScreen and PlaceBlock consume a hotbar selection; mirrors the C# HotbarSelectActionHandler gate
export function uiStateAcceptsHotbarSelect(state: string | null): boolean {
  return state === UiStateNames.gameScreen || state === UiStateNames.placeBlock;
}

// grab は掴んだ絵が見える画面でしか成立しない。クリック可否と GrabOverlay 描画の単一の正
// A grab only holds where the held item is visible; single source for clickability and GrabOverlay
export function screenAllowsGrab(screen: UiScreen): boolean {
  return screen === "playerInventory" || screen === "subInventory" || screen === "researchTree";
}

// 常時表示族は研究画面でだけ引っ込む
// The always-on family withdraws only on the research screen
// 常駐チャレンジHUDと採掘進捗バーはこの族に含まず、研究画面でも出したままにする
// The resident challenge HUD and the mining progress bar are not in this family and stay visible there
export function screenShowsAlwaysOnHud(screen: UiScreen): boolean {
  return screen !== "researchTree";
}
