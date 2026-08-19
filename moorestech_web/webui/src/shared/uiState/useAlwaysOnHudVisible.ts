import { Topics, useTopicSelector } from "@/bridge";
import { screenForUiState, screenShowsAlwaysOnHud } from "./uiScreenRouting";

// 常時表示族のHUDが自分で引っ込むための購読。画面名はここだけが知る
// Subscription that lets the always-on HUDs withdraw themselves; only this file knows the screen names
export function useAlwaysOnHudVisible(): boolean {
  return useTopicSelector(Topics.uiState, (d) => screenShowsAlwaysOnHud(screenForUiState(d?.state ?? null, d?.subState)));
}
