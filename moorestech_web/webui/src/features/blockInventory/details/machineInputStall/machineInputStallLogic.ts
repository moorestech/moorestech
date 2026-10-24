import type { MachineInputStallData } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// 止まった理由ごとの文言キー
// The message key for each stall reason
const STALL_REASON_KEYS: Record<MachineInputStallData["reason"], TranslationKey> = {
  mixedLevel: L.ui.blockInventory.machineInputStallMixedLevel,
  aboveCraftCap: L.ui.blockInventory.machineInputStallAboveCraftCap,
};
export function machineInputStallMessageKey(stall: MachineInputStallData): TranslationKey {
  return STALL_REASON_KEYS[stall.reason];
}
