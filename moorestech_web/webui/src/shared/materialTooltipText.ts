import { useCallback } from "react";
import { L, useI18n, useItemDisplayName } from "@/shared/i18n";
import { formatSlotAmount } from "@/shared/ui/slotAmountFormat";

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
  const itemDisplayName = useItemDisplayName();

  // 数量を語る表示は同一スロット内で表記が割れないよう全て共通整形を通す
  // Every display that speaks a quantity goes through the shared formatting so one slot never spells numbers two ways
  return useCallback((key, itemId, requiredCount, ownedCount) => t(key, {
    itemName: itemDisplayName(itemId),
    ownedCount: formatSlotAmount(ownedCount),
    requiredCount: formatSlotAmount(requiredCount),
  }), [t, itemDisplayName]);
}
