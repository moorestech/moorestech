import { useTopicSelector, Topics } from "@/bridge";
import { screenForUiState } from "./uiScreenRouting";

// 画面が開いている間だけカーソルが解放されクリックが届く。常時表示HUDのクリック可否をここへ一元化する
// The cursor is released only while a screen is open, so every always-on HUD reads its clickability here
export function useScreenInteractive(): boolean {
  return useTopicSelector(Topics.uiState, (d) => screenForUiState(d?.state ?? null) !== "none");
}
