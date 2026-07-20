import { z } from "zod";

export const ModalRequestSchema = z.object({
  id: z.string(),
  title: z.string(),
  message: z.string(),
  buttonText: z.string(),
  variant: z.enum(["confirm", "error"]),
  input: z.boolean().optional(),
});
export const ModalDataSchema = z.object({ modal: ModalRequestSchema.optional() });
export const ProgressDataSchema = z.object({
  visible: z.boolean(), progress: z.number(), label: z.string().optional(),
});

// 未知のstate名は画面ルータが安全側へ処理するため文字列全体を受理する
// Accept every state name because the screen router handles unknown names safely
export const UiStateDataSchema = z.object({
  state: z.string(),
  subState: z.enum(["GameScreen", "PauseMenuScreen"]).optional(),
});
export const TrainRidingDataSchema = z.object({
  riding: z.boolean(),
  branchCandidateCount: z.number().int().nonnegative(),
  selectedBranchIndex: z.number().int().nonnegative(),
});
export const LocalizationDataSchema = z.object({ locale: z.string().min(1) });
export const PauseMenuDataSchema = z.object({ disconnected: z.boolean() });
export const PlacementModeDataSchema = z.object({
  selectedName: z.string(),
  height: z.number().int(),
  unavailableReason: z.string(),
  energizedRangeVisible: z.boolean(),
});
export const DeleteModeDataSchema = z.object({ unavailableReason: z.string() });
export const KeyHintsDataSchema = z.object({ textKey: z.string() });
export const CrosshairDataSchema = z.object({ visible: z.boolean() });
export const UiVisibilityDataSchema = z.object({ visible: z.boolean() });
export const MiningHudDataSchema = z.object({
  visible: z.boolean(), targetName: z.string(), mining: z.boolean(), progress: z.number().min(0).max(1),
});
export const TooltipDataSchema = z.object({ visible: z.boolean(), textKey: z.string(), fontSize: z.number().positive() });
