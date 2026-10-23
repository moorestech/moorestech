import type { SpaceElevatorStatus } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// 状態ごとの表示キーと、足りない色で出すか
// Display key per status and whether it shows in the shortage color
export type SpaceElevatorStatusDisplay = { labelKey: TranslationKey; insufficient: boolean };

const StatusDisplayTable: Record<SpaceElevatorStatus, SpaceElevatorStatusDisplay> = {
  readyToConnect: { labelKey: L.ui.blockInventory.spaceElevatorStatusReadyToConnect, insufficient: false },
  insufficientCable: { labelKey: L.ui.blockInventory.spaceElevatorStatusInsufficientCable, insufficient: true },
  waitingSatellite: { labelKey: L.ui.blockInventory.spaceElevatorStatusWaitingSatellite, insufficient: false },
  climbing: { labelKey: L.ui.blockInventory.spaceElevatorStatusClimbing, insufficient: false },
  lowPower: { labelKey: L.ui.blockInventory.spaceElevatorStatusLowPower, insufficient: true },
  outputBlocked: { labelKey: L.ui.blockInventory.spaceElevatorStatusOutputBlocked, insufficient: true },
};

export function spaceElevatorStatusDisplay(status: SpaceElevatorStatus): SpaceElevatorStatusDisplay {
  return StatusDisplayTable[status];
}
