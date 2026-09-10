import { useCallback } from "react";
import { useItemMaster } from "@/bridge";
import { itemNameKey } from "../contentKeys";
import { L } from "../generated/localizationKeys";
import { useI18n } from "../i18nStore";

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

// 未解決時のid表示への落とし込みをここ1箇所へ閉じる
// Closes the unresolved-name fallback into this single place
export function useItemDisplayName(): (itemId: number) => string {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();

  return useCallback(
    (itemId: number) => resolveItemName(itemId) ?? t(L.ui.common.itemFallback, { itemId }),
    [resolveItemName, t],
  );
}
