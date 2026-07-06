import { dispatchAction } from "@/bridge";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import { pickUpPayload, placePayload } from "./blockLogic";
import { useBlockInteraction } from "./blockInteractionContext";

// itemSlots を 9 幅グリッドで描画し grab/move_item と連動する共通部品
// 9-wide grid of a block's itemSlots, wired to grab via move_item
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  // grab と名前解決は panel が context で供給する。送信は dispatchAction を直接呼ぶ（memo 安定のため context 外）
  // grab and name resolution come from the panel's context; dispatch calls dispatchAction directly (outside context for stable memo)
  const { grabCount, resolveName } = useBlockInteraction();
  const grabHeld = grabCount > 0;

  // grab 保持時は置く、空かつ中身ありなら拾う。表示更新は event 駆動に委ねる
  // Place while holding grab; pick up when empty and the slot has items. Rendering follows topic events
  const onLeftDown = (index: number, slot: SlotData) => {
    if (grabHeld) {
      void dispatchAction("block_inventory.move_item", placePayload(index, grabCount));
      return;
    }
    if (slot.count === 0) return;
    void dispatchAction("block_inventory.move_item", pickUpPayload(index, slot.count));
  };

  return (
    <SlotGrid testId={testId}>
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          name={resolveName(slot.itemId)}
          onLeftDown={() => onLeftDown(index, slot)}
        />
      ))}
    </SlotGrid>
  );
}
