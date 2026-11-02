import { Stack, Text } from "@mantine/core";
import { useI18n, type TranslationKey } from "@/shared/i18n";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { SlotData } from "@/bridge";
import { useBlockSlotGestures } from "./useBlockSlotGestures";

const MAX_BLOCK_INVENTORY_COLUMNS = 9;

// itemSlots を最大9列で描画し grab/move_item/collect と連動する共通部品
// Grid of up to 9 columns for block itemSlots, wired to grab via move_item/collect
export default function BlockItemGrid({ itemSlots, slotIndices, testId }: { itemSlots: SlotData[]; slotIndices: number[]; testId: string }) {
  // ジェスチャ配線は useBlockSlotGestures に共通化（MachineSection の分割グリッドと同一挙動）
  // Gesture wiring is shared via useBlockSlotGestures (identical to MachineSection's split grids)
  const gestures = useBlockSlotGestures();

  return (
    <SlotGrid cols={Math.min(MAX_BLOCK_INVENTORY_COLUMNS, Math.max(1, slotIndices.length))} testId={testId}>
      {slotIndices.map((index) => (
        <ItemSlot
          key={index}
          testId={`${testId}-slot-${index}`}
          itemId={itemSlots[index].itemId}
          count={itemSlots[index].count}
          onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
          onRightDown={() => gestures.onRightDown(index)}
          onDoubleClick={() => gestures.onDoubleClick(index)}
        />
      ))}
    </SlotGrid>
  );
}

// 用途ラベルの見た目を入出力グリッド間で揃える
// Keep purpose labels consistent across input and output grids
export function BlockItemGridGroup({ labelKey, ...grid }: {
  labelKey: TranslationKey;
  itemSlots: SlotData[];
  slotIndices: number[];
  testId: string;
}) {
  const { t } = useI18n();
  return (
    <Stack gap={4}>
      <Text size="sm" c="var(--text-muted)">{t(labelKey)}</Text>
      <BlockItemGrid {...grid} />
    </Stack>
  );
}
