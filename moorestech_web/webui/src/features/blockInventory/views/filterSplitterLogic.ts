import type { FilterSplitterMode } from "@/bridge/contract/payloadTypes";

export type FilterSlotClickAction = "set" | "clear" | "noop";

// uGUI と同じモード循環
// Uses the same mode cycle as uGUI
export function nextMode(mode: FilterSplitterMode): FilterSplitterMode {
  if (mode === "default") return "whitelist";
  if (mode === "whitelist") return "blacklist";
  return "default";
}

// grab 空の設定は無操作
// Empty-grab assign clicks are no-ops
export function filterSlotClickAction(grabCount: number, clear: boolean): FilterSlotClickAction {
  if (clear) return "clear";
  if (grabCount > 0) return "set";
  return "noop";
}

export const modeLabel: Record<FilterSplitterMode, string> = {
  default: "デフォルト",
  whitelist: "ホワイトリスト",
  blacklist: "ブラックリスト",
};
