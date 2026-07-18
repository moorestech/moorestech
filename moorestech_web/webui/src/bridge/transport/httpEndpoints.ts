export const itemMasterUrl = "/api/master/items";

export function itemIconUrl(itemId: number): string {
  return `/api/icons/${itemId}.png`;
}

export function blockIconUrl(blockId: number): string {
  return `/api/block-icons/${blockId}.png`;
}
