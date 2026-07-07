import type { ActionPayloads } from "@/bridge/transport/protocol";

// 「送るべき action」の計画表現。type と payload の対応が型で相関する分配ユニオン
// A planned action to send; a distributive union correlating type with its payload
export type PlannedAction = {
  [K in keyof ActionPayloads]: { type: K; payload: ActionPayloads[K] };
}[keyof ActionPayloads];
