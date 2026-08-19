import { Topics, useTopicSelector } from "@/bridge";
import { screenForUiState, screenShowsAlwaysOnHud } from "./uiScreenRouting";

// 常時表示HUD購読。画面名はここだけ知る
// Subscription for always-on HUDs; only this file knows screen names
export function useAlwaysOnHudVisible(): boolean {
  return useTopicSelector(Topics.uiState, (d) => screenShowsAlwaysOnHud(screenForUiState(d?.state ?? null, d?.subState)));
}
