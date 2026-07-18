import { readTopic, Topics } from "@/bridge";
import type { SlotData } from "@/bridge";
import {
  dispatchPlanned,
  planBlockDoubleClick,
  planBlockLeftClick,
  planBlockRightClick,
} from "@/shared/itemMove";
import { useBlockInteraction } from "./blockInteractionContext";

export type BlockSlotGestures = {
  onLeftDown: (index: number, slot: SlotData, shiftKey: boolean) => void;
  onRightDown: (index: number, slot: SlotData) => void;
  onDoubleClick: (index: number) => void;
};

// ブロックスロット共通のジェスチャ配線。グリッド形状に依存しないため任意レイアウト（機械の分割グリッド等）で使える
// Shared gesture wiring for block slots; layout-agnostic so split grids like machines reuse it
export function useBlockSlotGestures(): BlockSlotGestures {
  const { grabCount, resolveMaxStack } = useBlockInteraction();

  const onLeftDown = (index: number, slot: SlotData, shiftKey: boolean) => {
    // 最新の main スロットは event 時点で readTopic から読む（購読による再レンダー増を避ける）
    // Read the latest main slots via readTopic at event time (avoids extra re-renders from subscribing)
    const inventory = readTopic(Topics.inventory);
    if (!inventory) return;
    dispatchPlanned(
      planBlockLeftClick(index, slot, shiftKey, {
        grabCount,
        maxStack: resolveMaxStack(slot.itemId),
        mainSlots: inventory.mainSlots,
      }),
    );
  };

  const onRightDown = (index: number, slot: SlotData) => {
    dispatchPlanned(planBlockRightClick(index, slot, grabCount));
  };

  const onDoubleClick = (index: number) => {
    dispatchPlanned(planBlockDoubleClick(index));
  };

  return { onLeftDown, onRightDown, onDoubleClick };
}
