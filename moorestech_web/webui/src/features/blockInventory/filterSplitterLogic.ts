import type { FilterSplitterMode } from "@/bridge";

export type FilterSlotClickAction = "set" | "clear" | "noop";

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
