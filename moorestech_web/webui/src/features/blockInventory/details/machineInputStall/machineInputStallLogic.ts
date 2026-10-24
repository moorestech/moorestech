import type { MachineInputStallData } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// 止まった理由ごとの文言キー
// The message key for each stall reason
export function machineInputStallMessageKey(stall: MachineInputStallData): TranslationKey {
  switch (stall.reason) {
    case "mixedLevel":
      return L.ui.blockInventory.machineInputStallMixedLevel;
    case "aboveCraftCap":
      return L.ui.blockInventory.machineInputStallAboveCraftCap;
  }
}
