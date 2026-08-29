import type { BuildMenuRequiredItem } from "../../../bridge/contract/payloadTypes";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

// 表示上の不足は「素材が足りない」かつ「支払いが免除されていない」。合成はここが唯一の場所
// A displayed shortage means the material is short and the payment is not waived; this is the only place they compose
export function isItemInsufficient(entry: BuildMenuDisplayEntry, item: BuildMenuRequiredItem): boolean {
  return !entry.paymentWaived && item.lacking;
}

// ツールチップに出す不足行。免除中は1件も出さない
// The shortage rows for the tooltip; a waived entry yields none
export function insufficientItems(entry: BuildMenuDisplayEntry): BuildMenuRequiredItem[] {
  return entry.requiredItems.filter((item) => isItemInsufficient(entry, item));
}
