import { useEffect, useState } from "react";
import { subscribeTopic } from "./webSocketClient";

// 指定トピックを購読して最新の値を返す React フック
// React hook that subscribes to a topic and returns the latest value
export function useTopic<T>(topic: string): T | null {
  const [value, setValue] = useState<T | null>(null);
  useEffect(() => {
    const unsub = subscribeTopic<T>(topic, (data) => setValue(data));
    return unsub;
  }, [topic]);
  return value;
}
