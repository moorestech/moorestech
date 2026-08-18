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

// 素材ツールチップ共通部: itemNameフォールバック+所持数解決だけを集約する。
// 文言キー自体は呼び出し側(craft/research)ごとに分かれるため引数で受け取る(D2/D7)
// Shared material-tooltip piece: only itemName fallback + owned-count resolution are centralized.
// The wording key itself differs per caller (craft/research), so it's passed in (D2/D7)
export function useMaterialTooltipText(): (key: TranslationKey, itemId: number, requiredCount: number, owned: Map<number, number>) => string {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();

  return useCallback((key, itemId, requiredCount, owned) => t(key, {
    itemName: resolveItemName(itemId) ?? t(L.ui.common.itemFallback, { itemId }),
    ownedCount: ownedCountOf(owned, itemId),
    requiredCount,
  }), [t, resolveItemName]);
}
