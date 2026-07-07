import { dispatchAction } from "@/bridge";
import type { ActionPayloads } from "@/bridge";
import type { PlannedAction } from "./plannedAction";

// 相関の取れた type/payload 組だけを受けるヘルパ。union のまま dispatchAction へ渡すための橋
// Helper taking only correlated type/payload pairs; bridges the union into dispatchAction
const dispatchOne = <K extends keyof ActionPayloads>(action: { type: K; payload: ActionPayloads[K] }) =>
  dispatchAction(action.type, action.payload);

// 計画された action 列を順に送信する。ack を待たず投げ切り、表示更新は topic event に委ねる
// Fire the planned actions in order without awaiting acks; rendering follows topic events
export function dispatchPlanned(planned: PlannedAction[]): void {
  for (const action of planned) void dispatchOne(action);
}
