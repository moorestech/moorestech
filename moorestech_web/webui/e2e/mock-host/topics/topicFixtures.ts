import { Topics } from "../../../src/bridge/transport/protocol";
import type { TopicPayloads } from "../../../src/bridge/transport/protocol";
import type { PlayerInventoryData, BlockInventoryWireData } from "../../../src/bridge/contract/payloadTypes";
import * as fx from "../fixtures";
import { state } from "../state";

export const demoMode = process.env.MOCK_DEMO === "1";

// snapshot 生成に必要な接続ローカル状態。inventory は接続ごとに分離されている
// Connection-local state needed to build a snapshot; inventory is isolated per connection
type SnapshotContext = { inventory: PlayerInventoryData; demo: boolean };

// topic → snapshot 生成の型付きレジストリ。mock fixture の形状ずれをコンパイル時に検出する
// blockInventoryだけはクライアント側パース前のワイヤ形式を送るため、そこだけ型を差し替える
// Typed topic → snapshot registry; makes mock fixture shape drift a compile error
// blockInventory alone sends the pre-parse wire shape, so it overrides the payload type
type TopicFixtureRegistry = {
  [K in keyof TopicPayloads]: (context: SnapshotContext) => K extends typeof Topics.blockInventory ? BlockInventoryWireData : TopicPayloads[K];
};

const topicFixtures: TopicFixtureRegistry = {
  [Topics.inventory]: ({ inventory }) => inventory,
  [Topics.craftRecipes]: () => fx.craftRecipes,
  [Topics.machineRecipes]: () => fx.machineRecipes,
  [Topics.itemList]: ({ demo }) => (demo ? fx.demoItemList : fx.itemList),
  [Topics.blockInventory]: () => state.currentBlock,
  // ModalData.modal は optional（null 不可）のため型適合で undefined 化。ワイヤ上の null 除去は wire.ts の stripNulls が担う
  // Coerce to undefined to satisfy the optional (non-nullable) ModalData.modal; wire.ts stripNulls owns null removal on the wire
  [Topics.modal]: () => ({ modal: state.currentModal ?? undefined }),
  // 実ホストの NotificationTopic と同じ空snapshot。返さないと restoring のままで操作が塞がる
  // Same empty snapshot as the real NotificationTopic; without it the client stays restoring and input is blocked
  [Topics.notification]: () => ({}),
  [Topics.progress]: ({ demo }) => (demo ? fx.demoProgress : fx.progressSample),
  [Topics.uiState]: () => state.currentUiState,
  [Topics.researchTree]: () => state.researchTree,
  [Topics.buildMenu]: () => fx.buildMenu,
  [Topics.hotbar]: () => fx.hotbar,
  [Topics.localization]: () => ({ locale: "japanese", revision: 1 }),
  // 通常のe2eは出展モードではないので待機しない。欠けるとsnapshotが返らずrestoringのまま全操作が塞がる
  // Regular e2e is not event mode, so it never waits; a missing entry returns no snapshot and wedges everything in restoring
  [Topics.eventLanguageGate]: () => ({ waiting: false }),
  [Topics.challengeTree]: () => fx.challengeTree,
  [Topics.challengeCurrent]: () => fx.challengeCurrent,
  [Topics.pauseMenu]: () => ({ disconnected: false }),
  [Topics.placementMode]: () => ({
    selectedTargetType: "raw", selectedName: "", height: 0, unavailableReason: "", wheelOwnedByTool: false,
  }),
  [Topics.crosshair]: () => ({ visible: true }),
  [Topics.uiVisibility]: () => ({ visible: true }),
  [Topics.tooltip]: () => ({ visible: false, lines: [] }),
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
  // prototype キー（toString 等）を既知 topic と誤認しないよう own property のみ引く
  // Look up own properties only so prototype keys (toString etc.) are not mistaken for known topics
  if (!Object.hasOwn(topicFixtures, topic)) return undefined;
  return topicFixtures[topic as keyof TopicPayloads]({ inventory, demo });
}
