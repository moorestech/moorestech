import { z } from "zod";

// 継承しないレシピも同じキーで運ぶ（-1・空配列・0）。Web側で既定値を推測させない
// Non-inheritance recipes ride the same keys (-1, empty arrays, 0) so the web never guesses defaults
const NoInheritanceSchema = z.object({
  kind: z.literal("none"),
  sourceInputIndex: z.literal(-1),
  sourceItemIds: z.array(z.number().int()).length(0),
  targetOutputIndex: z.literal(-1),
  targetItemIds: z.array(z.number().int()).length(0),
  maxCraftableLevel: z.literal(0),
}).strict();

// 継承するレシピは実在する入出力の番号・空でない変種の並び・1以上の上限を持つ。none の番兵値は受けない
// An inheriting recipe carries real input/output indices, non-empty variant lists and a cap of at least 1; none's sentinels are rejected
const inheritingShape = {
  sourceInputIndex: z.number().int().nonnegative(),
  sourceItemIds: z.array(z.number().int()).min(1),
  targetOutputIndex: z.number().int().nonnegative(),
  targetItemIds: z.array(z.number().int()).min(1),
  maxCraftableLevel: z.number().int().positive(),
};
export const MachineRecipeLevelInheritanceSchema = z.discriminatedUnion("kind", [
  NoInheritanceSchema,
  z.object({ kind: z.literal("inherit"), ...inheritingShape }).strict(),
  z.object({ kind: z.literal("inheritUpToUnlocked"), ...inheritingShape }).strict(),
]);

// モジュール1スロットのスタック上限
// Per-module-slot stack limit
export const MachineUpgradeDataSchema = z.object({
  moduleSlotStackLimit: z.number().int().positive(),
}).strict();
