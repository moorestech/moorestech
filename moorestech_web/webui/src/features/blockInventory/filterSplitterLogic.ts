import type { FilterSplitterMode } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

export type FilterSlotClickAction = "set" | "clear" | "noop";

// grab 空の設定は無操作
// Empty-grab assign clicks are no-ops
export function filterSlotClickAction(grabCount: number, clear: boolean): FilterSlotClickAction {
  if (clear) return "clear";
  if (grabCount > 0) return "set";
  return "noop";
}

export function filterModeTranslationKey(mode: FilterSplitterMode): TranslationKey {
  if (mode === "default") return L.ui.blockInventory.filterDefault;
  if (mode === "whitelist") return L.ui.blockInventory.filterWhitelist;
  return L.ui.blockInventory.filterBlacklist;
}
