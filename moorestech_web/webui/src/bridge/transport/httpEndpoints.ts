export const itemMasterUrl = "/api/master/items";

// アイコン経路 prefix は mock host も同じ定数を参照する（並行実装の禁止）
// Icon path prefixes are shared with the mock host (no parallel implementation)
export const ITEM_ICON_PREFIX = "/api/icons/";
export const BLOCK_ICON_PREFIX = "/api/block-icons/";

export function itemIconUrl(itemId: number): string {
  return `${ITEM_ICON_PREFIX}${itemId}.png`;
}

export function blockIconUrl(blockId: number): string {
  return `${BLOCK_ICON_PREFIX}${blockId}.png`;
}

export function localizationDictionaryUrl(locale: string): string {
  return `/api/i18n/${encodeURIComponent(locale)}`;
}
