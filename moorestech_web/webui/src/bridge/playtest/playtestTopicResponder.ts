import { useTopicStore } from "../store/topicStore";
import { subscriptions } from "../transport/subscriptionManager";

const responders = new Map<string, () => void>();

type TopicState = ReturnType<typeof useTopicStore.getState>;

// 復元完了後だけ一時イベントを購読し、新しい要求を渡す
// Subscribe to transient events after restoration and pass along new requests
export function initPlaytestTopicResponder(topic: string, handle: (request: unknown) => void): () => void {
  const existing = responders.get(topic);
  if (existing !== undefined) return existing; // silent-ok: 同じ購読を返す / 呼出元が同じ解除操作を受け取る
  let topicAcquired = false;
  const unsubscribeStore = useTopicStore.subscribe((state, previousState) => {
    syncTopicSubscription(state.status);
    const request = state.topics[topic];
    if (request !== previousState.topics[topic]) handle(request);
  });
  syncTopicSubscription(useTopicStore.getState().status);

  const dispose = () => {
    // silent-ok: 古い解除操作は新登録を保持する / 現登録の解除操作が所有する
    if (responders.get(topic) !== dispose) return;
    responders.delete(topic);
    unsubscribeStore();
    if (topicAcquired) subscriptions.release(topic);
  };

  responders.set(topic, dispose);
  return dispose;

  function syncTopicSubscription(status: TopicState["status"]) {
    // 切断時は解除し、次回の復元対象への混入を防ぐ
    // Release on disconnect to keep the event out of the next restore set
    if (status === "open" && !topicAcquired) {
      topicAcquired = true;
      subscriptions.acquire(topic);
    } else if (status !== "open" && topicAcquired) {
      topicAcquired = false;
      subscriptions.release(topic);
    }
  }
}
