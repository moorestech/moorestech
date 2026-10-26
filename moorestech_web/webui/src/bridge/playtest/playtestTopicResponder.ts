import { useTopicStore } from "../store/topicStore";
import { subscriptions } from "../transport/subscriptionManager";

type TopicState = ReturnType<typeof useTopicStore.getState>;

// 復元完了後だけ一時イベントを購読し、新しい要求を渡す
// Subscribe to transient events after restoration and pass along new requests
export function initPlaytestTopicResponder(topic: string, handle: (request: unknown) => void): () => void {
  let topicAcquired = false;
  let disposed = false;
  const unsubscribeStore = useTopicStore.subscribe((state, previousState) => {
    syncTopicSubscription(state.status);
    const request = state.topics[topic];
    if (request !== previousState.topics[topic]) handle(request);
  });
  syncTopicSubscription(useTopicStore.getState().status);

  return () => {
    if (disposed) return;
    disposed = true;
    unsubscribeStore();
    if (topicAcquired) subscriptions.release(topic);
  };

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
