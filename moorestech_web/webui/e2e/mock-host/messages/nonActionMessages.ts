import type { WebSocket } from "ws";
import type { ClientMsg } from "../../../src/bridge/transport/protocol";
import type { PlayerInventoryData } from "../../../src/bridge/contract/payloadTypes";
import { send } from "../wire";
import { state, topicSubscribers } from "../state";
import { demoMode, topicData } from "../topics/topicFixtures";

// action以外の制御を処理し処理済みならtrue
// Handles non-action transport control, returns consumed
export function handleNonActionMessage(ws: WebSocket, msg: ClientMsg, inv: PlayerInventoryData): boolean {
  if (msg.op === "ping") {
    send(ws, { op: "pong" });
    return true;
  }
  // 入力排他はUnity側だけの関心事で、mockには反映先がない
  // Input exclusivity concerns only Unity; the mock has no state to apply it to
  if (msg.op === "input_state") return true;
  if (msg.op === "subscribe") {
    for (const topic of msg.topics) {
      const subscribers = topicSubscribers.get(topic) ?? new Set();
      subscribers.add(ws);
      topicSubscribers.set(topic, subscribers);
      const data = topicData(topic, inv, demoMode);
      if (data !== undefined) {
        const deliver = () => send(ws, { op: "snapshot", topic, data });
        if (state.snapshotDelayMs > 0 && (state.snapshotDelayTopic === null || state.snapshotDelayTopic === topic)) setTimeout(deliver, state.snapshotDelayMs);
        else deliver();
      }
    }
    return true;
  }
  if (msg.op !== "unsubscribe") return false;

  for (const topic of msg.topics) topicSubscribers.get(topic)?.delete(ws);
  return true;
}
