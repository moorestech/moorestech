import { useTopicSelector, Topics } from "@/bridge";
import { screenAllowsGrab, screenForUiState } from "./uiScreenRouting";

// いま開いている画面で grab が成立するか。スロットのクリック可否と GrabOverlay 描画はこの1つだけを読む
// Whether the current screen lets a grab hold; slot clickability and GrabOverlay rendering both read only this
export function useGrabInteractive(): boolean {
  return useTopicSelector(Topics.uiState, (d) => screenAllowsGrab(screenForUiState(d?.state ?? null, d?.subState)));
}
