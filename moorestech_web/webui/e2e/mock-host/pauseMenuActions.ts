import type { PlayerInventoryData } from "../../src/bridge/contract/payloadTypes";
import { Topics } from "../../src/bridge/transport/protocol";
import type { ActionPayloads, PauseMenuPageName } from "../../src/bridge/transport/protocol";
import { send } from "./wire";
import { state, subscribersOf } from "./state";
import { demoMode, topicData } from "./topics/topicFixtures";

// ページの書き込み口はここだけ。既定fixtureとoverrideの両方が同じページを配るよう揃える
// The only page writer: keeps the default fixture and any override serving the same page
export function setPauseMenuPage(page: PauseMenuPageName): void {
  state.pauseMenuPage = page;
  const pauseMenuOverride = state.topicOverrides.get(Topics.pauseMenu) as { page?: string } | undefined;
  if (pauseMenuOverride) state.topicOverrides.set(Topics.pauseMenu, { ...pauseMenuOverride, page });
}

// 指定ページへ遷移しfixtureと購読者へ反映
// Moves to the page, reflects state in fixture and subscribers
function applyPauseMenuShowPage(inv: PlayerInventoryData, payload: ActionPayloads["pause_menu.show_page"]): void {
  setPauseMenuPage(payload.page);
  setTimeout(() => {
    const data = topicData(Topics.pauseMenu, inv, demoMode);
    for (const sub of subscribersOf(Topics.pauseMenu)) send(sub, { op: "event", topic: Topics.pauseMenu, data });
  }, 30);
}

export type PauseMenuActionResult = { handled: false } | { handled: true };

// ポーズ画面の状態変更を反映
// Applies pause-page state changes
export function applyPauseMenuAction(inv: PlayerInventoryData, type: string, payload: unknown): PauseMenuActionResult {
  // 設定の変更も本物と同じtopic経由で選択へ反映する
  // Reflect setting changes through the topic, matching the real host
  if (type === "settings.setSprintInputMode") {
    const data = { ...payload as ActionPayloads["settings.setSprintInputMode"], recoveredInvalidPreference: false };
    state.topicOverrides.set(Topics.sprintInputMode, data);
    for (const sub of subscribersOf(Topics.sprintInputMode)) send(sub, { op: "event", topic: Topics.sprintInputMode, data });
    return { handled: true };
  }
  if (type === "pause_menu.show_page") {
    applyPauseMenuShowPage(inv, payload as ActionPayloads["pause_menu.show_page"]);
    return { handled: true };
  }
  // 再送・書き出しは受理するだけ。状態遷移はC#側の責務
  // Resend and export are only accepted; state transitions belong to C#
  if (type === "bug_report.resend_failed" || type === "bug_report.export_unsent") return { handled: true };
  if (type === "bug_report.submit") {
    applyPauseMenuShowPage(inv, { page: "top" });
    return { handled: true };
  }
  return { handled: false };
}
