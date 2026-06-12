// /api/master/items の手書き型
// Handwritten types for /api/master/items

export type ItemMasterEntry = { itemId: number; name: string; maxStack: number };

export type ItemMasterData = { items: ItemMasterEntry[] };
