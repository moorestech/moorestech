import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge";
import { useBlockInteraction } from "./blockInteractionContext";
import { useBlockSlotGestures } from "./useBlockSlotGestures";

// itemSlots を 9 幅グリッドで描画し grab/move_item/collect と連動する共通部品
// 9-wide grid of a block's itemSlots, wired to grab via move_item/collect
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  const { resolveName } = useBlockInteraction();
  // ジェスチャ配線は useBlockSlotGestures に共通化（MachineSection の分割グリッドと同一挙動）
  // Gesture wiring is shared via useBlockSlotGestures (identical to MachineSection's split grids)
  const gestures = useBlockSlotGestures();

  return (
    <SlotGrid testId={testId}>
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          name={resolveName(slot.itemId)}
          onLeftDown={(shiftKey) => gestures.onLeftDown(index, slot, shiftKey)}
          onRightDown={() => gestures.onRightDown(index, slot)}
          onDoubleClick={() => gestures.onDoubleClick(index)}
        />
      ))}
    </SlotGrid>
  );
}
