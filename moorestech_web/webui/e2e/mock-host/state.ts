import type { WebSocket } from "ws";
import type { BlockInventoryData, ModalRequest, UiStateData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { clone } from "./wire";

// 受信 action を記録（送信契約 assert 用に /__actions で返す）
// Record received actions (exposed at /__actions to assert the send contract)
export const received: { type: string; payload: unknown }[] = [];

// topic ごとの購読者集合。HTTP 制御エンドポイントと WS handler の双方が参照する
// Per-topic subscriber sets, shared by both the HTTP control endpoints and the WS handler
export const blockSubscribers = new Set<WebSocket>();
export const modalSubscribers = new Set<WebSocket>();
export const uiStateSubscribers = new Set<WebSocket>();

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
};
