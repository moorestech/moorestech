import { dispatchAction } from "@/bridge";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import { blockSlotClickPayload } from "./blockLogic";
import { useBlockInteraction } from "./blockInteractionContext";

// itemSlots を 9 幅グリッドで描画し grab/move_item と連動する共通部品
// 9-wide grid of a block's itemSlots, wired to grab via move_item
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  // grab と名前解決は panel が context で供給する。送信は dispatchAction を直接呼ぶ（memo 安定のため context 外）
  // grab and name resolution come from the panel's context; dispatch calls dispatchAction directly (outside context for stable memo)
  const { grabCount, resolveName } = useBlockInteraction();

  // クリック分岐は blockLogic に共通化。payload が null なら無操作、表示更新は event 駆動に委ねる
  // Click branching is shared in blockLogic; a null payload means no-op, and rendering follows topic events
  const onLeftDown = (index: number, slot: SlotData) => {
    const payload = blockSlotClickPayload(index, slot.itemId, slot.count, grabCount);
    if (payload) void dispatchAction("block_inventory.move_item", payload);
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
