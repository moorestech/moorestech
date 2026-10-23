import type { RocketLaunchPadData } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// 段階と待ち理由から状態文のキーを選ぶ。待ち理由があればそちらを優先する
// Choose the status text key from phase and wait reason, preferring the wait reason when present
export function rocketStatusKey(detail: RocketLaunchPadData): TranslationKey {
  switch (detail.waitReason) {
    case "missingMaterials": return L.ui.blockInventory.rocketWaitMissingMaterials;
    case "missingPayload": return L.ui.blockInventory.rocketWaitMissingPayload;
    case "outputFull": return L.ui.blockInventory.rocketWaitOutputFull;
    case "noPower": return L.ui.blockInventory.rocketWaitNoPower;
    case "none": break;
  }
  switch (detail.phase) {
    case "idle": return L.ui.blockInventory.rocketStatusIdle;
    case "awaitingBuildCommand": return L.ui.blockInventory.rocketStatusAwaitingBuild;
    case "building": return L.ui.blockInventory.rocketStatusBuilding;
    case "awaitingLaunchCommand": return L.ui.blockInventory.rocketStatusAwaitingLaunch;
    case "readyToLaunch": return L.ui.blockInventory.rocketStatusReadyToLaunch;
    case "launching": return L.ui.blockInventory.rocketStatusLaunching;
  }
}

// 合図のボタンは初打ち上げ前の合図待ちだけに出る
// Command buttons appear only while awaiting a command before the first launch
export function rocketCommandButton(detail: RocketLaunchPadData): "build" | "launch" | null {
  if (detail.phase === "awaitingBuildCommand") return "build";
  if (detail.phase === "awaitingLaunchCommand") return "launch";
  return null;
}
