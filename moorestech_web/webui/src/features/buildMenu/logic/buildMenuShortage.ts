import type { BuildMenuRequiredItem } from "@/bridge";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

// 不足の正本はホストのlacking。ここは絞り込むだけで判定しない
// The host's lacking flag is the source of truth; this only filters and never decides
export function shortageItemsOf(entry: BuildMenuDisplayEntry): BuildMenuRequiredItem[] {
  return entry.requiredItems.filter((item) => item.lacking);
}
