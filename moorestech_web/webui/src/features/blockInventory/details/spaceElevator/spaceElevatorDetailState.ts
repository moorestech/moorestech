import type { BlockInventoryOpen } from "@/bridge";

// wireの未着を専用ビュー入口で表示状態へ変換する
// Convert pending wire details into presentation state at the dedicated view boundary
export type SpaceElevatorDetailState =
  | { kind: "loading" }
  | { kind: "ready"; detail: NonNullable<BlockInventoryOpen["spaceElevator"]> };

export function toSpaceElevatorDetailState(data: BlockInventoryOpen): SpaceElevatorDetailState {
  return data.spaceElevator ? { kind: "ready", detail: data.spaceElevator } : { kind: "loading" };
}
