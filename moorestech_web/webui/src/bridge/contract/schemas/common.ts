import { z } from "zod";

export const SlotDataSchema = z.object({ itemId: z.number(), count: z.number() });
export const InventoryAreaSchema = z.enum(["main", "hotbar", "grab"]);
export const SlotRefSchema = z.object({ area: InventoryAreaSchema, slot: z.number() });
export const BlockInventoryAreaSchema = z.enum(["main", "hotbar", "grab", "block"]);
export const BlockSlotRefSchema = z.object({ area: BlockInventoryAreaSchema, slot: z.number() });
