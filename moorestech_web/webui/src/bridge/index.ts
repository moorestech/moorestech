// bridge の public API。feature 層はここ経由で通信境界へアクセスする（feature→bridge の一方向）
// Public API of bridge; the feature layer accesses the comm boundary through here (feature→bridge, one-way)
export { useTopic } from "./useTopic";
export { useItemMaster } from "./useItemMaster";
export { dispatchAction } from "./actions";
export { Topics } from "./protocol";
export type { TopicPayloads, ActionPayloads } from "./protocol";
export type * from "./payloadTypes";
