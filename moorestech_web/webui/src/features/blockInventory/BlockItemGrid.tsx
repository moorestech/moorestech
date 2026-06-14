import { ItemSlot } from "@/shared/ui";
import type { SlotData } from "@/bridge/payloadTypes";
import { pickUpPayload, placePayload } from "./blockLogic";
import { useBlockInteraction } from "./blockInteractionContext";

// block の itemSlots を 9 幅グリッドで描画し、プレイヤー grab と move_item で連動させる共通部品
// Shared 9-wide grid of a block's itemSlots, wired to the player grab via move_item
export default function BlockItemGrid({ itemSlots, testId }: { itemSlots: SlotData[]; testId: string }) {
  // grab/名前解決/送信は panel が context で供給する（bridge を直接 import せず node テスト可能に保つ）
  // grab/name-resolver/dispatch are supplied by the panel via context (no direct bridge import, keeps node tests viable)
  const { grabCount, resolveName, dispatch } = useBlockInteraction();
  const grabHeld = grabCount > 0;

  // grab 保持時は置く、空かつ中身ありなら拾う。表示更新は event 駆動に委ねる
  // Place while holding grab; pick up when empty and the slot has items. Rendering follows topic events
  const onLeftDown = (index: number, slot: SlotData) => {
    if (grabHeld) {
      dispatch(placePayload(index, grabCount));
      return;
    }
    if (slot.count === 0) return;
    dispatch(pickUpPayload(index, slot.count));
  };

  return (
    <div data-testid={testId} className="grid grid-cols-9 gap-1 w-fit">
      {itemSlots.map((slot, index) => (
        <ItemSlot
          key={index}
          itemId={slot.itemId}
          count={slot.count}
          name={resolveName(slot.itemId)}
          onLeftDown={() => onLeftDown(index, slot)}
        />
      ))}
    </div>
  );
}
