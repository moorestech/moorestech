import type { PlayerInventoryData } from "../../src/bridge/contract/payloadTypes";
import { Topics } from "../../src/bridge/transport/protocol";
import type { ActionPayloads } from "../../src/bridge/transport/protocol";
import { send } from "./wire";
import { state, subscribersOf } from "./state";
import { demoMode, topicData } from "./topics/topicFixtures";

// 指定ページへ遷移し、上書き中のfixtureと全購読者へ同じ状態を反映する
// Move to the requested page and reflect the same state in the overridden fixture and every subscriber
function applyPauseMenuShowPage(inv: PlayerInventoryData, payload: ActionPayloads["pause_menu.show_page"]): void {
  state.pauseMenuPage = payload.page;
  const pauseMenuOverride = state.topicOverrides.get(Topics.pauseMenu) as { page?: string } | undefined;
  if (pauseMenuOverride) state.topicOverrides.set(Topics.pauseMenu, { ...pauseMenuOverride, page: state.pauseMenuPage });
  setTimeout(() => {
    const data = topicData(Topics.pauseMenu, inv, demoMode);
    for (const sub of subscribersOf(Topics.pauseMenu)) send(sub, { op: "event", topic: Topics.pauseMenu, data });
  }, 30);
}

export type PauseMenuActionResult = { handled: false } | { handled: true; payload?: unknown };

// ポーズメニューactionの状態変更と成功応答payloadを同じ境界で組み立てる
// Build pause-menu action state changes and success-response payloads at the same boundary
export function applyPauseMenuAction(inv: PlayerInventoryData, type: string, payload: unknown): PauseMenuActionResult {
  if (type === "pause_menu.show_page") {
    applyPauseMenuShowPage(inv, payload as ActionPayloads["pause_menu.show_page"]);
    return { handled: true };
  }
  if (type === "bug_report.submit") {
    applyPauseMenuShowPage(inv, { page: "top" });
    return { handled: true, payload: { missing: [] } };
  }
  return { handled: false };
}
