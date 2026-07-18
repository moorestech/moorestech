// bridge の public API。feature 層はここ経由で通信境界へアクセスする（feature→bridge の一方向）
// Public API of bridge; the feature layer accesses the comm boundary through here (feature→bridge, one-way)
export { useTopic, useTopicSelector, useConnectionStatus, readTopic } from "./store/useTopic";
export { useItemMaster, readItemMaster } from "./store/useItemMaster";
export { dispatchAction } from "./transport/actions";
export { blockIconUrl, itemIconUrl, itemMasterUrl, localizationDictionaryUrl } from "./transport/httpEndpoints";
export { setToastSink } from "./transport/notify";
export type { NotifyVariant } from "./transport/notify";
export { Topics, UiStateNames } from "./transport/protocol";
export type { TopicPayloads, ActionPayloads } from "./transport/protocol";
export { initBridge, sendInputState } from "./transport/webSocketClient";
export type * from "./contract/payloadTypes";
