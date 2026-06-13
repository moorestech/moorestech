import { useEffect, useState } from "react";
import { subscribeTopic } from "./webSocketClient";
import type { TopicPayloads } from "./protocol";

// 指定トピックを購読して最新の値を返す React フック（初回 snapshot 前は null）
// React hook that subscribes to a topic and returns the latest value (null before the first snapshot)
export function useTopic<K extends keyof TopicPayloads>(topic: K): TopicPayloads[K] | null {
  const [value, setValue] = useState<TopicPayloads[K] | null>(null);
  useEffect(() => {
    const unsub = subscribeTopic(topic, (data) => setValue(data));
    return unsub;
  }, [topic]);
  return value;
}
