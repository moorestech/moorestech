import { useCallback } from "react";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";

// 素材ツールチップを名乗る辞書キーだけを受ける。任意のキーを所持数語彙で解釈させない
// Accepts only the keys that claim to be material tooltips; no arbitrary key gets the owned-count vocabulary
export type MaterialTooltipKey =
  | typeof L.ui.recipe.materialTooltip
  | typeof L.ui.research.consumeItemTooltip
  | typeof L.ui.buildMenu.materialTooltip
  | typeof L.ui.buildMenu.materialShortageLine;

// 素材ツールチップ共通部(itemName+所持数+必要数)
// Shared material-tooltip piece (itemName + owned count + required count)
export function useMaterialTooltipText(): (key: MaterialTooltipKey, itemId: number, requiredCount: number, ownedCount: number) => string {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();

  return useCallback((key, itemId, requiredCount, ownedCount) => t(key, {
    itemName: resolveItemName(itemId) ?? t(L.ui.common.itemFallback, { itemId }),
    ownedCount,
    requiredCount,
  }), [t, resolveItemName]);
}
