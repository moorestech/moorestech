import { z } from "zod";
import { GuidSchema } from "./common";
import { BuildMenuEntryKindSchema } from "./buildMenu";

// build_menu.entries と同じid+kind語彙を共有する。labelはC#側でDisplayName(現在ロケール)を都度解決し配信する
// Shares the same id+kind vocabulary with build_menu.entries; label is resolved server-side (DisplayName, current locale) each push
export const HotbarSlotSchema = z.object({
  id: GuidSchema,
  kind: BuildMenuEntryKindSchema,
  label: z.string().min(1),
  iconUrl: z.string().optional(),
});

// 9枠固定。未割当/未解決(未解放・削除済みBP等)の枠はnull
// Fixed 9 slots; unassigned or unresolved (locked/deleted blueprint, etc.) slots are null
export const HotbarDataSchema = z.object({
  slots: z.array(HotbarSlotSchema.nullable()).length(9),
  selectedSlot: z.number().int(),
});
