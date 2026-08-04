import { useCallback } from "react";
import { useItemMaster } from "@/bridge";
import { itemNameKey } from "../contentKeys";
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
