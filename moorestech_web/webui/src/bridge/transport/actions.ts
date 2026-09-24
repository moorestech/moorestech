import { sendAction } from "./webSocketClient";
import { notify } from "./notify";
import { parseActionResultPayload } from "../contract/validators";
import type { ActionPayloads, ActionResultPayloads } from "./protocol";

// 全画面ゲートの「すでに応答済み」を表す拒否コード。ゲートはこの1表から文言を選ぶ
// The rejection code meaning "already answered" for a full-screen gate; the gate picks its copy from this single table
export const GATE_ALREADY_ANSWERED_ERRORS = {
  "event_mode.select_language": "already_selected",
  "playtest.crash_report.respond": "already_responded",
  "playtest.consent.acknowledge": "already_acknowledged",
} as const;

export type GateAnswerActionType = keyof typeof GATE_ALREADY_ANSWERED_ERRORS;

// stale state 由来のクリック連鎖失敗は良性で、後続の topic event が再同期する。action type ごとに抑止コードを定義する
// Click-chain failures from stale state are benign and reconciled by a later topic event; suppress codes per action type
// これ以外（invalid_* 等の実バグ由来）はトーストする。ここに載るコードは共有 error_codes.json の部分集合であること
// Anything else (genuine failures like invalid_*) still toasts; codes here must stay a subset of the shared error_codes.json
export const BENIGN_ERRORS: Partial<Record<keyof ActionPayloads, ReadonlySet<string>>> = {
  "inventory.move_item": new Set(["empty_slot", "insufficient_count"]),
  "inventory.split": new Set(["grab_not_empty", "empty_slot"]),
  "block_inventory.move_item": new Set(["empty_slot", "insufficient_count"]),
  "block_inventory.split": new Set(["grab_not_empty", "empty_slot"]),
  "ui.modal.respond": new Set(["no_pending_modal"]),
  // メニューが先に閉じた/BPが先に消えた stale クリックはトースト不要
  // Stale clicks (menu already closed / BP already deleted) need no error toast
  "build_menu.select": new Set(["invalid_state", "unknown_entry"]),
  // ゲーム画面以外でHUDを叩いた良性の空振りはトースト不要
  // A benign miss from clicking the HUD outside the game screen needs no toast
  "hotbar.select": new Set(["invalid_state"]),
  // 二重右クリック等でサーバーが既にNotFoundを返す stale 削除はトースト不要（通信失敗は別コードで従来通りトーストする）
  // A stale delete where the server already returns NotFound (e.g. double right-click) needs no toast; communication failure keeps toasting under a separate code
  "blueprint.delete": new Set(["blueprint_delete_not_found"]),
  // 全画面ゲートの二重応答はサーバーが答えを持っており、ゲートが受理として扱う。トーストはゲートの下に隠れて誤報になる
  // A second gate answer finds the server already holding one and the gate treats it as accepted; a toast would be a hidden false alarm
  "event_mode.select_language": new Set([GATE_ALREADY_ANSWERED_ERRORS["event_mode.select_language"]]),
  "playtest.crash_report.respond": new Set([GATE_ALREADY_ANSWERED_ERRORS["playtest.crash_report.respond"]]),
  "playtest.consent.acknowledge": new Set([GATE_ALREADY_ANSWERED_ERRORS["playtest.consent.acknowledge"]]),
};

// 既定の待ち時間。UI操作は即応するので、これを超えたら通信が壊れている
// Default wait: a UI action answers immediately, so exceeding this means the transport is broken
export const DEFAULT_ACTION_TIMEOUT_MS = 5000;

// 既定を超えて時間がかかることが分かっている action だけを個別に延ばす
// Only actions known to take longer than the default get their own wait
// bug_report.submit は ffmpeg 結合・未追跡ファイルのコピー・git 4回起動を待つ。既定では成功を失敗と表示していた
// bug_report.submit waits on ffmpeg concat, untracked file copies and four git spawns; the default reported successes as failures
// playtest.crash_report.respond は録画リング・スナップショット・パケットログ・ダンプの同期コピーを待つ（実測28MB級）
// playtest.crash_report.respond waits on synchronous copies of the recording ring, snapshot, packet log and dump (28MB class in practice)
export const ACTION_TIMEOUTS_MS: Partial<Record<keyof ActionPayloads, number>> = {
  "bug_report.submit": 120000,
  "playtest.crash_report.respond": 120000,
};

export function shouldToastFailure(type: keyof ActionPayloads, error: string | undefined): boolean {
  if (error === undefined) return true;
  return !(BENIGN_ERRORS[type]?.has(error) ?? false);
}

// 失敗を真偽値へ潰さずに受け取るための結果型。「サーバーが断った」と「届かなかった」は別の対処になる
// Outcome type that keeps failures out of a boolean: "the server refused" and "it never arrived" call for different handling
type ActionOutcome<TPayload = never> =
  | ([TPayload] extends [never] ? { kind: "accepted" } : { kind: "accepted"; payload: TPayload })
  | { kind: "rejected"; error: string }
  | { kind: "unreachable"; reason: "timeout" | "disconnected" | "other" };

type ActionOutcomeFor<K extends keyof ActionPayloads> =
  K extends keyof ActionResultPayloads ? ActionOutcome<ActionResultPayloads[K]> : ActionOutcome;

// action を発行し、失敗時はトースト表示して理由つきの結果を返す。理由まで要る画面だけがこちらを呼ぶ
// Dispatch an action, toast on failure and return the outcome with its reason; only screens that need the reason call this
// accepted は「サーバーが受理した」ことを意味し、topic event の反映完了を保証しない
// accepted means the server accepted the action; it does not guarantee the topic event has arrived yet
export function dispatchActionOutcome<K extends keyof ActionPayloads>(
  type: K,
  payload: ActionPayloads[K],
): Promise<ActionOutcomeFor<K>>;
export async function dispatchActionOutcome<K extends keyof ActionPayloads>(
  type: K,
  payload: ActionPayloads[K],
): Promise<ActionOutcome | ActionOutcome<unknown>> {
  try {
    const result = await sendAction(type, payload, ACTION_TIMEOUTS_MS[type] ?? DEFAULT_ACTION_TIMEOUT_MS);
    if (result.ok) {
      const parsed = parseActionResultPayload(type, result.payload);
      if (!parsed.registered) return { kind: "accepted" };
      if (parsed.valid) return { kind: "accepted", payload: parsed.value };
      // 契約違反を欠損なしの成功へ縮退させず、開発者とユーザーの両方へ失敗を示す
      // Do not degrade a contract violation into gap-free success; expose failure to both developers and users
      console.warn("Action returned an invalid registered success payload", { type, payload: result.payload });
      notify(`${type} failed: invalid_response`, "error");
      return { kind: "rejected", error: "invalid_response" };
    }
    const error = result.error ?? "unknown";
    if (shouldToastFailure(type, result.error)) notify(`${type} failed: ${error}`, "error");
    return { kind: "rejected", error };
  } catch (e) {
    // 切断中の失敗は再接続オーバーレイが状態を伝えるため個別トーストしない
    // Don't toast per-failure while disconnected; the reconnect overlay conveys the state
    const message = e instanceof Error ? e.message : String(e);
    if (message !== "disconnected") notify(`${type} error: ${message}`, "error");
    return { kind: "unreachable", reason: toUnreachableReason(message) };
  }
}

// action を発行し、受理されたかだけを返す UI 向けラッパ
// UI-facing wrapper: dispatch an action and report only whether it was accepted
export async function dispatchAction<K extends keyof ActionPayloads>(
  type: K,
  payload: ActionPayloads[K],
): Promise<boolean> {
  const outcome = await dispatchActionOutcome(type, payload);
  return outcome.kind === "accepted";
}

// 送信側が投げるのは webSocketClient の "timeout" / "disconnected" の2種で、それ以外は想定外の例外
// The sender throws webSocketClient's "timeout" / "disconnected"; anything else is an unexpected exception
function toUnreachableReason(message: string): "timeout" | "disconnected" | "other" {
  if (message === "timeout") return "timeout";
  if (message === "disconnected") return "disconnected";
  return "other";
}
