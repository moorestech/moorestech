import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge";
import { useBlockSlotGestures } from "./useBlockSlotGestures";

// itemSlots を最大9列で描画し grab/move_item/collect と連動する共通部品
// Grid of up to 9 columns for block itemSlots, wired to grab via move_item/collect
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  // ジェスチャ配線は useBlockSlotGestures に共通化（MachineSection の分割グリッドと同一挙動）
  // Gesture wiring is shared via useBlockSlotGestures (identical to MachineSection's split grids)
  const gestures = useBlockSlotGestures();

  return (
    <SlotGrid cols={Math.min(9, Math.max(1, itemSlots.length))} testId={testId}>
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
          onRightDown={() => gestures.onRightDown(index)}
          onDoubleClick={() => gestures.onDoubleClick(index)}
        />
      ))}
    </SlotGrid>
  );
}
