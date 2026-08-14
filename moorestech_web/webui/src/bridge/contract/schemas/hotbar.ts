import { z } from "zod";
import { GuidSchema } from "./common";

// build_menu.entries と同じ判別共用体。マスタ由来名はGuid導出キーで解決し、ホストから表示名を運ばない
// The same discriminated union as build_menu.entries: master-derived names resolve through GUID-derived keys without host-provided labels
const HotbarDictionaryResolvedSlotSchema = z.object({
  kind: z.enum(["block", "trainCar", "connectTool"]),
  id: GuidSchema,
  iconUrl: z.string(),
  label: z.never().optional(),
}).strict();

const HotbarBlueprintCopySlotSchema = z.object({
  kind: z.literal("blueprintCopy"),
  id: GuidSchema,
  iconUrl: z.never().optional(),
  label: z.never().optional(),
}).strict();

// ユーザー命名のBPだけが辞書キーを持たないため原文labelを運ぶ
// Only user-named blueprints lack a dictionary key, so they carry their raw label
const HotbarBlueprintSlotSchema = z.object({
  kind: z.literal("blueprint"),
  id: GuidSchema,
  iconUrl: z.never().optional(),
  label: z.string().min(1),
}).strict();

// 割当はあるが解決できない枠（未解放・削除済みBP）。表示は使用不可だがドラッグ元にはなる
// An assigned slot that cannot be resolved (locked target, deleted blueprint): shown as unusable but still a drag source
const HotbarUnresolvedSlotSchema = z.object({
  kind: z.literal("unresolved"),
  id: GuidSchema,
}).strict();

export const HotbarSlotSchema = z.discriminatedUnion("kind", [
  HotbarDictionaryResolvedSlotSchema,
  HotbarBlueprintCopySlotSchema,
  HotbarBlueprintSlotSchema,
  HotbarUnresolvedSlotSchema,
]);

// 9枠固定。未割当の枠だけがnull
// Fixed 9 slots; only unassigned slots are null
export const HotbarDataSchema = z.object({
  slots: z.array(HotbarSlotSchema.nullable()).length(9),
  selectedSlot: z.number().int(),
});
