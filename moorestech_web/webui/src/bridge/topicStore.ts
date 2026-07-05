import { create } from "zustand";
import { validateTopicPayload } from "./validators";
import { notify } from "./notify";

// 接続状態。connecting=初回接続前, open=接続中, reconnecting=一度接続した後の切断中
// Connection status: connecting=before first connect, open=connected, reconnecting=dropped after a prior connect
export type ConnectionStatus = "connecting" | "open" | "reconnecting";

// topic 最新値と接続状態を持つ単一ストア。書き込みは WS 層のみ、React はセレクタで読む一方通行
// Single store for latest topic values and connection status; only the WS layer writes, React reads via selectors (one-way)
type TopicState = {
  topics: Record<string, unknown>;
  status: ConnectionStatus;
  setTopic: (topic: string, data: unknown) => void;
  setStatus: (status: ConnectionStatus) => void;
};

export const useTopicStore = create<TopicState>((set) => ({
  topics: {},
  status: "connecting",
  setTopic: (topic, data) => set((s) => ({ topics: { ...s.topics, [topic]: data } })),
  setStatus: (status) => set({ status }),
}));

// WS 受信の唯一の書き込み口。バリデーション通過時のみストアへ反映し、違反は警告+toastで破棄する
// The sole write path for WS input; store only on valid payloads, drop violations with a warn + toast
export function deliverTopicPayload(topic: string, data: unknown): boolean {
  if (!validateTopicPayload(topic, data)) {
    console.warn(`[topicStore] dropped invalid payload for topic ${topic}`, data);
    notify(`Invalid data received for ${topic}`);
    return false;
  }
  useTopicStore.getState().setTopic(topic, data);
  return true;
}
