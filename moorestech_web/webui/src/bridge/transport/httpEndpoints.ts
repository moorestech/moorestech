export const itemMasterUrl = "/api/master/items";
export const fluidMasterUrl = "/api/master/fluids";
export const localizationLanguagesUrl = "/api/i18n-languages";

// アイコン経路 prefix は mock host も同じ定数を参照する（並行実装の禁止）
// Icon path prefixes are shared with the mock host (no parallel implementation)
export const ITEM_ICON_PREFIX = "/api/icons/";
export const BLOCK_ICON_PREFIX = "/api/block-icons/";
export const FLUID_ICON_PREFIX = "/api/fluid-icons/";

export function itemIconUrl(itemId: number): string {
  return `${ITEM_ICON_PREFIX}${itemId}.png`;
}

export function blockIconUrl(blockId: number): string {
  return `${BLOCK_ICON_PREFIX}${blockId}.png`;
}

export function fluidIconUrl(fluidGuid: string): string {
  return `${FLUID_ICON_PREFIX}${fluidGuid}.png`;
}

export function localizationDictionaryUrl(locale: string, revision: number): string {
  return `/api/i18n/${encodeURIComponent(locale)}?revision=${revision}`;
}
