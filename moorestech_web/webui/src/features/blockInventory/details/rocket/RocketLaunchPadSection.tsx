import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n, type TranslationKey } from "@/shared/i18n";
import { GaugeBar, ItemSlot, SlotGrid } from "@/shared/ui";
import { useBlockSlotGestures } from "../../useBlockSlotGestures";
import { rocketStatusKey } from "./rocketLaunchPadLogic";

export default function RocketLaunchPadSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const gestures = useBlockSlotGestures();
  const detail = data.rocketLaunchPad;
  // silent-ok: 初期状態の未着だけ描画を待つ / 次のtopicでパネルに表示される
  if (!detail) return null;

  // 絶対添字のスロットをジェスチャ付きで描く
  // Render a slot by absolute index with gestures attached
  const slotAt = (index: number) => {
    const slot = data.itemSlots[index];
    return (
      <ItemSlot
        key={index}
        itemId={slot.itemId}
        count={slot.count}
        onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
        onRightDown={() => gestures.onRightDown(index)}
        onDoubleClick={() => gestures.onDoubleClick(index)}
      />
    );
  };
  const group = (labelKey: TranslationKey, indices: number[], testId: string) => (
    <Stack gap={4}>
      <Text size="sm" c="var(--text-muted)">{t(labelKey)}</Text>
      <SlotGrid cols={Math.min(9, Math.max(1, indices.length))} testId={testId}>{indices.map(slotAt)}</SlotGrid>
    </Stack>
  );

  return (
    <Stack gap="sm">
      {group(L.ui.blockInventory.rocketMaterials, detail.materialSlots, "rocket-launch-pad-material-slots")}
      {group(L.ui.blockInventory.rocketPayloads, detail.payloadSlots, "rocket-launch-pad-payload-slots")}
      <Text size="sm" c="var(--text-high-contrast)" data-testid="rocket-launch-pad-status">{t(rocketStatusKey(detail))}</Text>
      <GaugeBar value={detail.progress} testId="rocket-launch-pad-progress" />
      {group(L.ui.blockInventory.rocketOutputs, detail.outputSlots, "rocket-launch-pad-output-slots")}
    </Stack>
  );
}
