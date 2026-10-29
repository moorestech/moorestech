import { Topics, type TopicPayloads } from "../../../src/bridge/transport/protocol";

import { topicData } from "./topicFixtures";
import { inventory } from "../fixtures";

type Delivery = TopicPayloads[typeof Topics.pauseMenu]["delivery"];
// 実配信と同じ形で状態別画面を再現する
// Reproduce each delivery state using the production payload shape
function pause(delivery: Delivery, disconnected: boolean) {
  return { kind: "set" as const, topic: Topics.pauseMenu, data: { disconnected, delivery, export: { kind: "idle" as const }, bugReport: { kind: "ready" as const, missing: [] }, page: "top" as const } };
}
// 通常fixtureを再利用し、復旧状態だけを試験用に切り替える
// Reuse the normal fixture and vary only recovery state for tests
function graphicsRecovery(recovered: boolean) {
  const data = topicData(Topics.graphicsQuality, inventory, false) as TopicPayloads[typeof Topics.graphicsQuality];
  return { kind: recovered ? "set" as const : "clear" as const, topic: Topics.graphicsQuality, data: { ...data, recoveredInvalidPreference: recovered } };
}
export const pauseMenuControls = {
  graphicsRecovered: () => graphicsRecovery(true),
  graphicsRecoveredClear: () => graphicsRecovery(false),
  pauseConnected: () => pause({ kind: "allSent" }, false),
  pauseDisconnected: () => pause({ kind: "allSent" }, true),
  deliveryMissing: () => pause({ kind: "localOnly", reason: "developerTokenMissing", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryUnreadable: () => pause({ kind: "localOnly", reason: "developerTokenUnreadable", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliverySteamStopped: () => pause({ kind: "localOnly", reason: "distributionUnavailable", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryPending: () => pause({ kind: "pending", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryRefused: () => pause({ kind: "refused", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryAllSent: () => pause({ kind: "allSent" }, false),
};
