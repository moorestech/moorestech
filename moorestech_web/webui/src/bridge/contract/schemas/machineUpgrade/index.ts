import { z } from "zod";

// 継承しないレシピも同じ形で運ぶ（-1・空配列・0）。Web側で既定値を推測させない
// Non-inheritance recipes ride the same shape (-1, empty arrays, 0) so the web never guesses defaults
export const MachineRecipeLevelInheritanceSchema = z.object({
  kind: z.enum(["none", "inherit", "inheritUpToUnlocked"]),
  sourceInputIndex: z.number().int(),
  sourceItemIds: z.array(z.number().int()),
  targetOutputIndex: z.number().int(),
  targetItemIds: z.array(z.number().int()),
  maxCraftableLevel: z.number().int().nonnegative(),
}).strict();

// モジュール1スロットのスタック上限
// Per-module-slot stack limit
export const MachineUpgradeDataSchema = z.object({
  moduleSlotStackLimit: z.number().int().positive(),
}).strict();
