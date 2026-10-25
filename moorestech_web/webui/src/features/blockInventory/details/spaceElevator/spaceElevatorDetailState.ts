import type { BlockInventoryOpen } from "@/bridge";

// wire未着をビューの表示状態へ変換
// Convert pending wire details into presentation state at the view boundary
export type SpaceElevatorDetailState =
  | { kind: "loading" }
  | { kind: "ready"; detail: NonNullable<BlockInventoryOpen["spaceElevator"]> };

export function toSpaceElevatorDetailState(data: BlockInventoryOpen): SpaceElevatorDetailState {
  return data.spaceElevator ? { kind: "ready", detail: data.spaceElevator } : { kind: "loading" };
}
