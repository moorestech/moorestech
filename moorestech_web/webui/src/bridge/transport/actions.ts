import { sendAction } from "./webSocketClient";
import { notify } from "./notify";
import type { ActionPayloads } from "./protocol";

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
};

// 既定の待ち時間。UI操作は即応するので、これを超えたら通信が壊れている
// Default wait: a UI action answers immediately, so exceeding this means the transport is broken
export const DEFAULT_ACTION_TIMEOUT_MS = 5000;

// 既定を超えて時間がかかることが分かっている action だけを個別に延ばす
// Only actions known to take longer than the default get their own wait
// bug_report.submit は ffmpeg 結合・未追跡ファイルのコピー・git 4回起動を待つ。既定では成功を失敗と表示していた
// bug_report.submit waits on ffmpeg concat, untracked file copies and four git spawns; the default reported successes as failures
export const ACTION_TIMEOUTS_MS: Partial<Record<keyof ActionPayloads, number>> = {
  "bug_report.submit": 120000,
};

export function shouldToastFailure(type: keyof ActionPayloads, error: string | undefined): boolean {
  if (error === undefined) return true;
  return !(BENIGN_ERRORS[type]?.has(error) ?? false);
}

// action を発行し、失敗時はトースト表示して false を返す UI 向けラッパ
// UI-facing wrapper: dispatch an action, toast on failure, return success flag
// true は「サーバーが受理した」ことを意味し、topic event の反映完了を保証しない
// true means the server accepted the action; it does not guarantee the topic event has arrived yet
export async function dispatchAction<K extends keyof ActionPayloads>(
  type: K,
  payload: ActionPayloads[K],
): Promise<boolean> {
  try {
    const result = await sendAction(type, payload, ACTION_TIMEOUTS_MS[type] ?? DEFAULT_ACTION_TIMEOUT_MS);
    if (!result.ok) {
      if (shouldToastFailure(type, result.error)) notify(`${type} failed: ${result.error ?? "unknown"}`, "error");
      return false;
    }
    return true;
  } catch (e) {
    // 切断中の失敗は再接続オーバーレイが状態を伝えるため個別トーストしない
    // Don't toast per-failure while disconnected; the reconnect overlay conveys the state
    const message = e instanceof Error ? e.message : String(e);
    if (message !== "disconnected") notify(`${type} error: ${message}`, "error");
    return false;
  }
}
