import type { RocketLaunchPadData } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

type Command = { value: "build" | "launch"; labelKey: TranslationKey };
const phases: Record<RocketLaunchPadData["phase"], { statusKey: TranslationKey; command: Command | null }> = {
  idle: { statusKey: L.ui.blockInventory.rocketStatusIdle, command: null },
  awaitingBuildCommand: { statusKey: L.ui.blockInventory.rocketStatusAwaitingBuild, command: { value: "build", labelKey: L.ui.blockInventory.rocketBuild } },
  building: { statusKey: L.ui.blockInventory.rocketStatusBuilding, command: null },
  awaitingLaunchCommand: { statusKey: L.ui.blockInventory.rocketStatusAwaitingLaunch, command: { value: "launch", labelKey: L.ui.blockInventory.rocketLaunch } },
  readyToLaunch: { statusKey: L.ui.blockInventory.rocketStatusReadyToLaunch, command: null },
  launching: { statusKey: L.ui.blockInventory.rocketStatusLaunching, command: null },
};
const waitReasons: Record<RocketLaunchPadData["waitReason"], TranslationKey | null> = {
  none: null,
  missingMaterials: L.ui.blockInventory.rocketWaitMissingMaterials,
  missingPayload: L.ui.blockInventory.rocketWaitMissingPayload,
  outputFull: L.ui.blockInventory.rocketWaitOutputFull,
  noPower: L.ui.blockInventory.rocketWaitNoPower,
};

// 段階と待ち理由から状態文のキーを選ぶ。待ち理由があればそちらを優先する
// Choose the status text key from phase and wait reason, preferring the wait reason when present
export function rocketStatusKey(detail: RocketLaunchPadData): TranslationKey {
  return waitReasons[detail.waitReason] ?? phases[detail.phase].statusKey;
}

// 合図のボタンは初打ち上げ前の合図待ちだけに出る
// Command buttons appear only while awaiting a command before the first launch
export function rocketCommandButton(detail: RocketLaunchPadData): Command | null {
  return phases[detail.phase].command;
}
