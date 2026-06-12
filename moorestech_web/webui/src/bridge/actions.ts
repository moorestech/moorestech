import { sendAction } from "./webSocketClient";
import { showToast } from "./toastBus";

// action を発行し、失敗時はトースト表示して false を返す UI 向けラッパ
// UI-facing wrapper: dispatch an action, toast on failure, return success flag
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
