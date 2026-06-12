// local_player.inventory トピックの手書き型（SourceGenerator 導入後に自動生成へ置換予定）
// Handwritten types for local_player.inventory (to be replaced by generated types later)

export type SlotData = { itemId: number; count: number };

export type PlayerInventoryData = {
  mainSlots: SlotData[];
  hotbarSlots: SlotData[];
  grab: SlotData;
};

export type InventoryArea = "main" | "hotbar" | "grab";

export type SlotRef = { area: InventoryArea; slot: number };
