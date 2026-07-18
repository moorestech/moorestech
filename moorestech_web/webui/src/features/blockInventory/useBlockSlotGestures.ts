import { readItemMaster, readTopic, Topics } from "@/bridge";
import {
  dispatchPlanned,
  planBlockDoubleClick,
  planBlockLeftClick,
  planBlockRightClick,
} from "@/shared/itemMove";

export type BlockSlotGestures = {
  onLeftDown: (index: number, shiftKey: boolean) => void;
  onRightDown: (index: number) => void;
  onDoubleClick: (index: number) => void;
};

// ブロックスロット共通のジェスチャ配線。グリッド形状に依存しないため任意レイアウト（機械の分割グリッド等）で使える
// Shared gesture wiring for block slots; layout-agnostic so split grids like machines reuse it
export function useBlockSlotGestures(): BlockSlotGestures {
  const onLeftDown = (index: number, shiftKey: boolean) => {
    // ブロック操作の全入力もイベント時 snapshot に揃える
    // Align every block interaction input to the event-time snapshots
    const inventory = readTopic(Topics.inventory);
    const blockInventory = readTopic(Topics.blockInventory);
    if (!inventory || !blockInventory?.open) return;
    const slot = blockInventory.itemSlots[index];
    dispatchPlanned(
      planBlockLeftClick(index, slot, shiftKey, {
        grabCount: inventory.grab.count,
        maxStack: readItemMaster()?.get(slot.itemId)?.maxStack,
        mainSlots: inventory.mainSlots,
      }),
    );
  };

  const onRightDown = (index: number) => {
    const inventory = readTopic(Topics.inventory);
    const blockInventory = readTopic(Topics.blockInventory);
    if (!inventory || !blockInventory?.open) return;
    dispatchPlanned(planBlockRightClick(index, blockInventory.itemSlots[index], inventory.grab.count));
  };

  const onDoubleClick = (index: number) => {
    dispatchPlanned(planBlockDoubleClick(index));
  };

  return { onLeftDown, onRightDown, onDoubleClick };
}
