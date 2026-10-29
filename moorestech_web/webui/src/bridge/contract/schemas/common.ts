import { z } from "zod";

export const GuidSchema = z.string().uuid();
export const SlotDataSchema = z.object({ itemId: z.number(), count: z.number() });
export const InventoryAreaSchema = z.enum(["main", "grab", "equipment"]);
export const SlotRefSchema = z.object({ area: InventoryAreaSchema, slot: z.number() });
export const BlockInventoryAreaSchema = z.union([InventoryAreaSchema, z.literal("block")]);
export const BlockSlotRefSchema = z.object({ area: BlockInventoryAreaSchema, slot: z.number() });

// 入れ口スロットが受け付けるアイテム。サーバーの配置可否と同じ定義からホストが配信し、Web側で規則を再導出しない
// Items an input slot accepts; the host publishes them from the server's own placement definition so the Web side never re-derives the rule
export const InputSlotBindingSchema = z.object({
  slot: z.number().int().nonnegative(),
  acceptedItemIds: z.array(z.number().int().nonnegative()),
}).strict();

// 受け付け情報は入れ口の群とちょうど1対1で対応させ、欠けた入れ口を「何でも可」と読ませない
// Bindings must cover the input slot group one-to-one so a missing inlet is never read as "accepts anything"
export function refineBindingsCoverInputSlots(bindings: { slot: number }[], inputSlots: number[], ctx: z.RefinementCtx): void {
  const bound = bindings.map((binding) => binding.slot).sort((a, b) => a - b);
  const inputs = [...inputSlots].sort((a, b) => a - b);
  if (bound.length === inputs.length && bound.every((slot, i) => slot === inputs[i])) return;
  ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["slotBindings"], message: "slotBindings must cover the input slots one-to-one" });
}
