import { Topics, type TopicPayloads } from "../../../src/bridge/transport/protocol";

type Delivery = TopicPayloads[typeof Topics.pauseMenu]["delivery"];
// 実配信と同じ形で状態別画面を再現する
// Reproduce each delivery state using the production payload shape
function pause(delivery: Delivery, disconnected: boolean) {
  return { kind: "set" as const, topic: Topics.pauseMenu, data: { disconnected, delivery, bugReport: { kind: "ready" as const, missing: [] }, page: "top" as const } };
}
export const pauseMenuControls = {
  pauseConnected: () => pause({ kind: "allSent" }, false),
  pauseDisconnected: () => pause({ kind: "allSent" }, true),
  deliveryMissing: () => pause({ kind: "localOnly", reason: "developerTokenMissing", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryUnreadable: () => pause({ kind: "localOnly", reason: "developerTokenUnreadable", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliverySteamStopped: () => pause({ kind: "localOnly", reason: "distributionUnavailable", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryPending: () => pause({ kind: "pending", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryRefused: () => pause({ kind: "refused", inventory: { kind: "known", pendingCount: 3, failedCount: 0 } }, false),
  deliveryAllSent: () => pause({ kind: "allSent" }, false),
};
