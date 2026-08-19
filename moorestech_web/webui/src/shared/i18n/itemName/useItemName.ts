import { useCallback } from "react";
import { useItemMaster } from "@/bridge";
import { itemNameKey } from "../contentKeys";
import { useI18n } from "../i18nStore";
import type { TranslationKey } from "../i18nStore";
import { L } from "../generated/localizationKeys";
import { ownedCountOf } from "@/shared/ownedCounts";

// itemIdからGuidを引き、現辞書で名前解決
// Resolve itemId through its GUID in the current dictionary
export function useItemNameResolver(): (itemId: number) => string | null {
  const { t } = useI18n();
  const itemMaster = useItemMaster();

  return useCallback((itemId: number) => {
    const itemGuid = itemMaster?.get(itemId)?.itemGuid;
    return itemGuid ? t(itemNameKey(itemGuid)) : null;
  }, [itemMaster, t]);
}

// 素材ツールチップ共通部(itemName+所持数)
// Shared material-tooltip piece (itemName + owned-count)
export function useMaterialTooltipText(): (key: TranslationKey, itemId: number, requiredCount: number, owned: Map<number, number>) => string {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();

  return useCallback((key, itemId, requiredCount, owned) => t(key, {
    itemName: resolveItemName(itemId) ?? t(L.ui.common.itemFallback, { itemId }),
    ownedCount: ownedCountOf(owned, itemId),
    requiredCount,
  }), [t, resolveItemName]);
}
