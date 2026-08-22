// bridge の public API。feature 層はここ経由で通信境界へアクセスする（feature→bridge の一方向）
// Public API of bridge; the feature layer accesses the comm boundary through here (feature→bridge, one-way)
export { useTopic, useTopicSelector, useTopicEvents, useConnectionStatus, readTopic } from "./store/useTopic";
export type { ConnectionStatus } from "./store/topicStore";
export { useItemMaster, readItemMaster } from "./store/useItemMaster";
export { useFluidMaster } from "./store/useFluidMaster";
export { dispatchAction } from "./transport/actions";
export { blockIconUrl, itemIconUrl, fluidIconUrl, itemMasterUrl, fluidMasterUrl, localizationDictionaryUrl, localizationLanguagesUrl } from "./transport/httpEndpoints";
export { setToastSink } from "./transport/notify";
export type { NotifyVariant } from "./transport/notify";
export { Topics, UiStateNames } from "./transport/protocol";
export type { TopicPayloads, ActionPayloads } from "./transport/protocol";
export { initBridge, sendInputState } from "./transport/webSocketClient";
export type * from "./contract/payloadTypes";
