import { UiStateNames } from "@/bridge";

// ui_state.current の state 名 → Web が描画する画面。App.tsx ルーティングの単一の正
// Maps ui_state.current's state name to the web screen; single source for App.tsx routing
export type UiScreen = "none" | "playerInventory" | "subInventory" | "researchTree" | "buildMenu";

export function screenForUiState(state: string | null): UiScreen {
  if (state === UiStateNames.playerInventory) return "playerInventory";
  if (state === UiStateNames.subInventory) return "subInventory";
  if (state === UiStateNames.researchTree) return "researchTree";
  if (state === UiStateNames.buildMenu) return "buildMenu";
  // GameScreen・未対応state・未受信はパネル無し（前方互換: 未知state名も安全側に倒す)
  // GameScreen, unsupported states and pre-snapshot are panel-less (forward-compat: unknown names fail safe)
  return "none";
}
