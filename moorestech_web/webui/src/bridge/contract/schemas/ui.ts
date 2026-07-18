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
export const UiStateDataSchema = z.object({ state: z.string() });
