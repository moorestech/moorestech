import type { PlayerInventoryData } from "../../src/bridge/contract/payloadTypes";
import { Topics } from "../../src/bridge/transport/protocol";
import type { ActionPayloads } from "../../src/bridge/transport/protocol";
import { send } from "./wire";
import { state, subscribersOf } from "./state";
import { demoMode, topicData } from "./topics/topicFixtures";

// 指定ページへ遷移し、上書き中のfixtureと全購読者へ同じ状態を反映する
// Move to the requested page and reflect the same state in the overridden fixture and every subscriber
export function applyPauseMenuShowPage(inv: PlayerInventoryData, payload: ActionPayloads["pause_menu.show_page"]): void {
  state.pauseMenuPage = payload.page;
  const pauseMenuOverride = state.topicOverrides.get(Topics.pauseMenu) as { page?: string } | undefined;
  if (pauseMenuOverride) state.topicOverrides.set(Topics.pauseMenu, { ...pauseMenuOverride, page: state.pauseMenuPage });
  setTimeout(() => {
    const data = topicData(Topics.pauseMenu, inv, demoMode);
    for (const sub of subscribersOf(Topics.pauseMenu)) send(sub, { op: "event", topic: Topics.pauseMenu, data });
  }, 30);
}
