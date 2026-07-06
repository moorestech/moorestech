// bridge の public API。feature 層はここ経由で通信境界へアクセスする（feature→bridge の一方向）
// Public API of bridge; the feature layer accesses the comm boundary through here (feature→bridge, one-way)
export { useTopic, useTopicSelector, useConnectionStatus, readTopic } from "./store/useTopic";
export { useItemMaster } from "./store/useItemMaster";
export { dispatchAction } from "./transport/actions";
export { Topics } from "./transport/protocol";
export type { TopicPayloads, ActionPayloads } from "./transport/protocol";
export type * from "./contract/payloadTypes";
