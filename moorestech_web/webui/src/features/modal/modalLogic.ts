import type { ModalRequest } from "@/bridge/contract/payloadTypes";
import type { ActionPayloads } from "@/bridge";

// modal 応答の action payload を組み立てる純関数。text は input モーダルの確定時のみ付与する。
// Pure builder for the modal-response payload; text accompanies only an input modal's confirm.
export function respondPayload(
  id: string,
  result: "confirm" | "cancel",
  text?: string,
): ActionPayloads["ui.modal.respond"] {
  return text === undefined ? { id, result } : { id, result, text };
}

// 入力必須モーダルは空白のみを確定不可にする（uGUI BlueprintNameInputView と同一検証）
// Input-required modals reject whitespace-only text (same validation as uGUI BlueprintNameInputView)
export function canConfirm(input: boolean | undefined, text: string): boolean {
  if (!input) return true;
  return text.trim().length > 0;
}

// variant ごとの Mantine ボタン色を返す。error は赤、confirm は青で uGUI の色分けに対応。
// Returns the Mantine button color per variant; error→red, confirm→blue, mirroring the uGUI styling.
export function buttonColor(variant: ModalRequest["variant"]): "red" | "blue" {
  return variant === "error" ? "red" : "blue";
}
