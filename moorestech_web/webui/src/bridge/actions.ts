import { sendAction } from "./webSocketClient";
import { notify } from "./notify";
import type { ActionPayloads } from "./protocol";

// インベントリ操作は楽観的に dispatch し topic event で再同期するため、
// クリック連鎖で生じる良性の失敗（stale な空スロット等）はトースト表示しない
// Inventory ops are optimistic and reconciled by topic events,
// so benign failures from click chains (e.g. a stale empty slot) are not toasted
export function shouldToastFailure(type: string): boolean {
  return !type.startsWith("inventory.");
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
      if (shouldToastFailure(type)) notify(`${type} failed: ${result.error ?? "unknown"}`);
      return false;
    }
    return true;
  } catch (e) {
    notify(`${type} error: ${e instanceof Error ? e.message : String(e)}`);
    return false;
  }
}
