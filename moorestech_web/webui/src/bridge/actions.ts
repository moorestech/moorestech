import { sendAction } from "./webSocketClient";
import { showToast } from "./toastBus";

// action を発行し、失敗時はトースト表示して false を返す UI 向けラッパ
// UI-facing wrapper: dispatch an action, toast on failure, return success flag
// true は「サーバーが受理した」ことを意味し、topic event の反映完了を保証しない
// true means the server accepted the action; it does not guarantee the topic event has arrived yet
export async function dispatchAction(type: string, payload: unknown): Promise<boolean> {
  try {
    const result = await sendAction(type, payload);
    if (!result.ok) {
      showToast(`${type} failed: ${result.error ?? "unknown"}`);
      return false;
    }
    return true;
  } catch (e) {
    showToast(`${type} error: ${e instanceof Error ? e.message : String(e)}`);
    return false;
  }
}
