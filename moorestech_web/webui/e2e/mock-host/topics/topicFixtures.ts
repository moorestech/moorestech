import { Topics } from "../../../src/bridge/transport/protocol";
import type { TopicPayloads } from "../../../src/bridge/transport/protocol";
import type { PlayerInventoryData } from "../../../src/bridge/contract/payloadTypes";
import * as fx from "../fixtures";
import { state } from "../state";

export const demoMode = process.env.MOCK_DEMO === "1";

// snapshot 生成に必要な接続ローカル状態。inventory は接続ごとに分離されている
// Connection-local state needed to build a snapshot; inventory is isolated per connection
type SnapshotContext = { inventory: PlayerInventoryData; demo: boolean };

// topic → snapshot 生成の型付きレジストリ。mock fixture の形状ずれをコンパイル時に検出する
// Typed topic → snapshot registry; makes mock fixture shape drift a compile error
type TopicFixtureRegistry = {
  [K in keyof TopicPayloads]: (context: SnapshotContext) => TopicPayloads[K];
};

const topicFixtures: TopicFixtureRegistry = {
  [Topics.inventory]: ({ inventory }) => inventory,
  [Topics.craftRecipes]: () => fx.craftRecipes,
  [Topics.machineRecipes]: () => fx.machineRecipes,
  [Topics.itemList]: ({ demo }) => (demo ? fx.demoItemList : fx.itemList),
  [Topics.blockInventory]: () => state.currentBlock,
  // 実ホストは NullValueHandling.Ignore で modal キーごと省略する
  // The real host omits the modal key entirely via NullValueHandling.Ignore
  [Topics.modal]: () => ({ modal: state.currentModal ?? undefined }),
  // 実ホストの NotificationTopic と同じ空snapshot。返さないと restoring のままで操作が塞がる
  // Same empty snapshot as the real NotificationTopic; without it the client stays restoring and input is blocked
  [Topics.notification]: () => ({}),
  [Topics.progress]: ({ demo }) => (demo ? fx.demoProgress : fx.progressSample),
  [Topics.uiState]: () => state.currentUiState,
  [Topics.researchTree]: () => state.researchTree,
  [Topics.buildMenu]: () => fx.buildMenu,
  [Topics.localization]: () => ({ locale: "japanese" }),
  [Topics.challengeTree]: () => fx.challengeTree,
  [Topics.challengeCurrent]: () => fx.challengeCurrent,
  [Topics.pauseMenu]: () => ({ disconnected: false }),
  [Topics.placementMode]: () => ({ selectedName: "", height: 0, unavailableReason: "" }),
  [Topics.deleteMode]: () => ({ unavailableReason: "" }),
  [Topics.crosshair]: () => ({ visible: true }),
  [Topics.uiVisibility]: () => ({ visible: true }),
  [Topics.tooltip]: () => ({ visible: false, textKey: "", fontSize: 14 }),
  [Topics.gameState]: () => state.gameState,
  [Topics.tutorialPresentation]: () => fx.tutorialPresentation,
  [Topics.worldPins]: () => state.worldPins,
  [Topics.skitPresentation]: () => state.skitPresentation,
  [Topics.trainRiding]: () => state.trainRiding,
};

// override 優先。snapshot を持たない topic（playtest.dom_query 等）はレジストリに無く undefined を返す
// Overrides win; topics without a snapshot (e.g. playtest.dom_query) are absent from the registry and yield undefined
export function topicData(topic: string, inventory: PlayerInventoryData, demo: boolean): unknown {
  if (state.topicOverrides.has(topic)) return state.topicOverrides.get(topic);
  const build = topicFixtures[topic as keyof TopicPayloads];
  return build?.({ inventory, demo });
}
