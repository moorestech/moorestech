import type { ModalRequest } from "@/bridge/contract/payloadTypes";
import type { ActionPayloads } from "@/bridge";

// modal 応答の action payload を組み立てる純関数。confirm/cancel を id 付きで返す。
// Pure builder for the modal-response action payload; returns the result with its id.
export function respondPayload(
  id: string,
  result: "confirm" | "cancel",
): ActionPayloads["ui.modal.respond"] {
  return { id, result };
}

// variant ごとの Mantine ボタン色を返す。error は赤、confirm は青で uGUI の色分けに対応。
// Returns the Mantine button color per variant; error→red, confirm→blue, mirroring the uGUI styling.
export function buttonColor(variant: ModalRequest["variant"]): "red" | "blue" {
  return variant === "error" ? "red" : "blue";
}
