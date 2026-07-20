import type { ActiveLayer } from "@/shared/uiState";

export function handleBlockInventoryKeydown(
  key: string,
  activeLayer: ActiveLayer,
  requestGameScreen: () => void,
): void {
  if (key !== "Escape" || activeLayer !== "blockInventory") return;
  requestGameScreen();
}
