import { useEffect, useRef } from "react";
import { useTopicStore } from "./topicStore";
import { subscriptions } from "../transport/subscriptionManager";
import type { TopicPayloads } from "../transport/protocol";

// 指定トピックを購読して最新の値を返す React フック（初回 snapshot 前は null）
// React hook that subscribes to a topic and returns the latest value (null before the first snapshot)
export function useTopic<K extends keyof TopicPayloads>(topic: K): TopicPayloads[K] | null {
  // マウント中だけ参照カウントを保持する。topic 変更時は旧 topic を release し新 topic を acquire する
  // Hold a refcount only while mounted; on topic change, release the old topic and acquire the new one
  useEffect(() => {
    subscriptions.acquire(topic);
    return () => subscriptions.release(topic);
  }, [topic]);

  // topic をキーに最新値を返す。キー毎保持なので topic 変更時に前値が残らない
  // Return the latest value keyed by topic; per-key storage means no stale value on topic change
  return useTopicStore((s) => (s.topics[topic] ?? null) as TopicPayloads[K] | null);
}

// topic の一部だけを購読して不要な再レンダーを避けるセレクタ版
// Selector variant that subscribes to a derived slice to avoid needless re-renders
// selector は安定値/プリミティブを返すこと（毎回新しいオブジェクトを返すと無限再レンダーになる）
// The selector must return a stable/primitive value (returning a fresh object each call causes an infinite re-render loop)
export function useTopicSelector<K extends keyof TopicPayloads, R>(
  topic: K,
  selector: (value: TopicPayloads[K] | null) => R,
): R {
  useEffect(() => {
    subscriptions.acquire(topic);
    return () => subscriptions.release(topic);
  }, [topic]);

  return useTopicStore((s) => selector((s.topics[topic] ?? null) as TopicPayloads[K] | null));
}

// 比較方法を明示するセレクタ版。複合値を返す場合も安定性を呼び出し側で保証できる
// Selector variant with explicit comparison, allowing callers to stabilize composite results
export function useTopicSelectorWithEquality<K extends keyof TopicPayloads, R>(
  topic: K,
  selector: (value: TopicPayloads[K] | null) => R,
  equality: (left: R, right: R) => boolean,
): R {
  const previous = useRef<{ value: R } | null>(null);
  useEffect(() => {
    subscriptions.acquire(topic);
    return () => subscriptions.release(topic);
  }, [topic]);

  return useTopicStore((state) => {
    const next = selector((state.topics[topic] ?? null) as TopicPayloads[K] | null);
    if (previous.current !== null && equality(previous.current.value, next)) return previous.current.value;
    previous.current = { value: next };
    return next;
  });
}

// 接続状態を購読するフック。feature/app 層は topicStore を直接触らずこれを使う
// Hook subscribing to connection status; feature/app layers use this instead of touching topicStore
export function useConnectionStatus() {
  return useTopicStore((s) => s.status);
}

/**
 * フック外から最新値を読む命令的アクセサ。購読は行わず、bridge 初期化時に pin 済みの topic にのみ使用できる。
 * Imperative latest-value accessor outside hooks. It does not subscribe and may only read topics pinned during bridge initialization.
 */
export function readTopic<K extends keyof TopicPayloads>(topic: K): TopicPayloads[K] | null {
  return (useTopicStore.getState().topics[topic] ?? null) as TopicPayloads[K] | null;
}
