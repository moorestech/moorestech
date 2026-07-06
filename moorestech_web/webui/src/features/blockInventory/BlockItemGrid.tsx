import { dispatchAction, readTopic, Topics } from "@/bridge";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge/contract/payloadTypes";
import { blockShiftMovePayloads, blockSlotClickPayload, blockSlotRightClickPayload } from "./blockLogic";
import { useBlockInteraction } from "./blockInteractionContext";

// itemSlots を 9 幅グリッドで描画し grab/move_item/collect と連動する共通部品
// 9-wide grid of a block's itemSlots, wired to grab via move_item/collect
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  // grab と名前/maxStack 解決は panel が context で供給する。送信は dispatchAction を直接呼ぶ（memo 安定のため context 外）
  // grab and name/maxStack resolution come from the panel's context; dispatch calls dispatchAction directly (outside context for stable memo)
  const { grabCount, resolveName, resolveMaxStack } = useBlockInteraction();

  // クリック分岐は blockLogic に共通化。payload が null なら無操作、表示更新は event 駆動に委ねる
  // Click branching is shared in blockLogic; a null payload means no-op, and rendering follows topic events
  const onLeftDown = (index: number, slot: SlotData, shiftKey: boolean) => {
    // grab 保持中の Shift は通常の置きと同じ（uGUI 同様 grab が優先）
    // Shift while holding grab behaves as a plain place (grab wins, matching uGUI)
    if (shiftKey && grabCount === 0 && slot.count > 0) {
      // 最新の main スロットは event 時点で readTopic から読む（購読による再レンダー増を避ける）
      // Read the latest main slots via readTopic at event time (avoids extra re-renders from subscribing)
      const inventory = readTopic(Topics.inventory);
      if (!inventory) return;
      const moves = blockShiftMovePayloads(index, slot.itemId, slot.count, inventory.mainSlots, resolveMaxStack(slot.itemId));
      for (const move of moves) void dispatchAction("block_inventory.move_item", move);
      return;
    }
    const payload = blockSlotClickPayload(index, slot.itemId, slot.count, grabCount);
    if (payload) void dispatchAction("block_inventory.move_item", payload);
  };

  const onRightDown = (index: number, slot: SlotData) => {
    const payload = blockSlotRightClickPayload(index, slot.itemId, slot.count, grabCount);
    if (payload) void dispatchAction("block_inventory.move_item", payload);
  };

  // 収集先（grab か クリックスロットか）は host が自身の現在 grab 状態で決める（inventory.collect と同じ規約）
  // The host decides the target (grab vs clicked slot) from its own grab state (same contract as inventory.collect)
  const onDoubleClick = (index: number) => {
    void dispatchAction("block_inventory.collect", { slot: { area: "block", slot: index } });
  };

  return (
    <SlotGrid testId={testId}>
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          name={resolveName(slot.itemId)}
          onLeftDown={(shiftKey) => onLeftDown(index, slot, shiftKey)}
          onRightDown={() => onRightDown(index, slot)}
          onDoubleClick={() => onDoubleClick(index)}
        />
      ))}
    </SlotGrid>
  );
}
