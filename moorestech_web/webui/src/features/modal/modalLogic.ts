import type { ModalRequest } from "@/bridge/payloadTypes";
import type { ActionPayloads } from "@/bridge";

// modal 応答の action payload を組み立てる純関数。confirm/cancel を id 付きで返す。
// Pure builder for the modal-response action payload; returns the result with its id.
export function respondPayload(
  id: string,
  result: "confirm" | "cancel",
): ActionPayloads["ui.modal.respond"] {
  return { id, result };
}

// variant ごとのボタン色を返す。error は赤、confirm は青で uGUI の色分けに対応。
// Returns the button color per variant; error→red, confirm→blue, mirroring the uGUI styling.
export function buttonClass(variant: ModalRequest["variant"]): string {
  const base =
    "w-full rounded px-4 py-2 text-sm font-bold text-white transition-colors";
  const accent =
    variant === "error"
      ? "bg-red-700 hover:bg-red-600"
      : "bg-blue-700 hover:bg-blue-600";
  return `${base} ${accent}`;
}
