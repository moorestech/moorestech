import { z } from "zod";

export const GuidSchema = z.string().uuid();
export const SlotDataSchema = z.object({ itemId: z.number(), count: z.number() });
export const InventoryAreaSchema = z.enum(["main", "grab", "equipment"]);
export const SlotRefSchema = z.object({ area: InventoryAreaSchema, slot: z.number() });
export const BlockInventoryAreaSchema = z.union([InventoryAreaSchema, z.literal("block")]);
export const BlockSlotRefSchema = z.object({ area: BlockInventoryAreaSchema, slot: z.number() });
