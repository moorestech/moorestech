import type { WebSocket } from "ws";
import type { BlockInventoryData, ModalRequest, UiStateData, ResearchTreeData, GameStateData, SkitPresentationData, TrainRidingData, PlayerInventoryData, WorldPinPresentationData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { clone } from "./wire";

// 受信 action を記録（送信契約 assert 用に /__actions で返す）
// Record received actions (exposed at /__actions to assert the send contract)
export const received: { type: string; payload: unknown }[] = [];

export const connections = new Set<WebSocket>();
export const topicSubscribers = new Map<string, Set<WebSocket>>();
export const subscribersOf = (topic: string) => topicSubscribers.get(topic) ?? new Set<WebSocket>();

// 再代入される可変状態を1オブジェクトに集約し、モジュール間で参照共有する
// Group reassignable mutable state in one object so modules share it by reference
export const state = {
  // 既定は閉ブロック。開いた panel が他テスト画面を覆う干渉を避ける
  // Default closed so an opened panel never overlays and interferes with other tests
  currentBlock: clone(fx.blockClosed) as BlockInventoryData,
  // 既定は非表示モーダル。全面 backdrop の干渉を避ける opt-in
  // Default hidden modal; opt-in to avoid the full-screen backdrop interfering
  currentModal: null as ModalRequest | null,
  // 既定はインベントリ画面。既存 e2e の表示前提を維持する
  // Default inventory screen; keeps existing e2e display assumptions
  currentUiState: clone(fx.uiState) as UiStateData,
  // research.complete でノードを completed 化し購読者へ push する可変ツリー
  // Mutable tree; research.complete flips a node to completed and pushes to subscribers
  researchTree: clone(fx.researchTree) as ResearchTreeData,
  gameState: clone(fx.gameState) as GameStateData,
  skitPresentation: clone(fx.skitPresentation) as SkitPresentationData,
  worldPins: clone(fx.worldPins) as WorldPinPresentationData,
  trainRiding: clone(fx.trainRiding) as TrainRidingData,
  topicOverrides: new Map<string, unknown>(),
  injectedActionError: null as { type: string; error: string } | null,
  snapshotDelayMs: 0,
  snapshotDelayTopic: null as string | null,
  rejectConnectionsUntil: 0,
  preserveInventoryOnDisconnect: false,
  reconnectInventory: null as PlayerInventoryData | null,
  itemMaster: clone(fx.itemMaster),
};
