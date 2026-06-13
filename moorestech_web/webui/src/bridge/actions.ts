import { sendAction } from "./webSocketClient";
import { notify } from "./notify";
import type { ActionPayloads } from "./protocol";

// インベントリ操作のクリック連鎖は stale state で良性の失敗を生む（topic event が再同期する）。
// これらの error code だけ抑止し、invalid_* 等の実バグ由来の失敗は従来どおりトーストする
// Click chains on inventory ops yield benign failures from stale state (topic events reconcile them).
// Suppress only those error codes; genuine failures (invalid_*, etc.) still toast as before
const BENIGN_INVENTORY_ERRORS = new Set(["empty_slot", "insufficient_count", "grab_not_empty"]);

export function shouldToastFailure(type: keyof ActionPayloads, error: string | undefined): boolean {
  return !(type.startsWith("inventory.") && error !== undefined && BENIGN_INVENTORY_ERRORS.has(error));
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
    const result = await sendAction(type, payload);
    if (!result.ok) {
      if (shouldToastFailure(type, result.error)) notify(`${type} failed: ${result.error ?? "unknown"}`);
      return false;
    }
    return true;
  } catch (e) {
    notify(`${type} error: ${e instanceof Error ? e.message : String(e)}`);
    return false;
  }
}
