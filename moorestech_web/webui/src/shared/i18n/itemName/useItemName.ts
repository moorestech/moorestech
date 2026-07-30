import { useCallback } from "react";
import { useItemMaster } from "@/bridge";
import { itemNameKey } from "../contentKeys";
import { useI18n } from "../i18nStore";

// itemIdからGuidを引き、描画時点の辞書で名前を解決する
// Resolve itemId through its Guid against the dictionary active for this render
export function useItemNameResolver(): (itemId: number) => string | null {
  const { t } = useI18n();
  const itemMaster = useItemMaster();

  return useCallback((itemId: number) => {
    const itemGuid = itemMaster?.get(itemId)?.itemGuid;
    return itemGuid ? t(itemNameKey(itemGuid)) : null;
  }, [itemMaster, t]);
}
