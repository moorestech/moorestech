import { create } from "zustand";
import { validateTopicPayload } from "../contract/validators";
import { notify } from "../transport/notify";

// 接続状態。connecting=初回接続前, open=接続中, reconnecting=一度接続した後の切断中
// Connection status: connecting=before first connect, open=connected, reconnecting=dropped after a prior connect
export type ConnectionStatus = "connecting" | "restoring" | "open" | "reconnecting";

// topic 最新値と接続状態を持つ単一ストア。書き込みは WS 層のみ、React はセレクタで読む一方通行
// Single store for latest topic values and connection status; only the WS layer writes, React reads via selectors (one-way)
type TopicState = {
  topics: Record<string, unknown>;
  revisions: Record<string, number>;
  restoringTopics: Set<string>;
  status: ConnectionStatus;
  setTopic: (topic: string, revision: number, data: unknown) => boolean;
  setStatus: (status: ConnectionStatus) => void;
  beginRestore: (topics: string[]) => void;
};

export const useTopicStore = create<TopicState>((set) => ({
  topics: {},
  revisions: {},
  restoringTopics: new Set(),
  status: "connecting",
  setTopic: (topic, revision, data) => {
    let accepted = false;
    set((state) => {
      if ((state.revisions[topic] ?? -1) >= revision) return state;
      accepted = true;
      const restoringTopics = new Set(state.restoringTopics);
      restoringTopics.delete(topic);
      return {
        topics: { ...state.topics, [topic]: data },
        revisions: { ...state.revisions, [topic]: revision },
        restoringTopics,
        status: state.status === "restoring" && restoringTopics.size === 0 ? "open" : state.status,
      };
    });
    return accepted;
  },
  setStatus: (status) => set({ status }),
  beginRestore: (topics) => set({
    revisions: {},
    restoringTopics: new Set(topics),
    status: topics.length === 0 ? "open" : "restoring",
  }),
}));

// 購読終了した topic の残値を削除し、命令的読み出しが stale 値を返すことを防ぐ
// Remove a released topic's retained value so imperative reads cannot return stale data
export function clearTopic(topic: string) {
  useTopicStore.setState((state) => {
    if (!(topic in state.topics)) return state;
    const topics = { ...state.topics };
    const revisions = { ...state.revisions };
    delete topics[topic];
    delete revisions[topic];
    return { topics, revisions };
  });
}

// WS 受信の唯一の書き込み口。バリデーション通過時のみストアへ反映し、違反は警告+toastで破棄する
// The sole write path for WS input; store only on valid payloads, drop violations with a warn + toast
export function deliverTopicPayload(topic: string, revision: number, data: unknown): boolean {
  if (!validateTopicPayload(topic, data)) {
    console.warn(`[topicStore] dropped invalid payload for topic ${topic}`, data);
    notify(`Invalid data received for ${topic}`, "error");
    return false;
  }
  return useTopicStore.getState().setTopic(topic, revision, data);
}
