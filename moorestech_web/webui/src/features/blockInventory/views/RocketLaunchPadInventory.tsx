import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n, type TranslationKey } from "@/shared/i18n";
import { GaugeBar } from "@/shared/ui";
import BlockItemGrid from "../BlockItemGrid";
import { rocketStatusKey } from "../details/rocket/rocketLaunchPadLogic";

export default function RocketLaunchPadInventory({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.rocketLaunchPad;
  // silent-ok: 初期状態の未着だけ描画を待つ / 次のtopicでパネルに表示される
  if (!detail) return null;

  const group = (labelKey: TranslationKey, indices: number[], testId: string) => (
    <Stack gap={4}>
      <Text size="sm" c="var(--text-muted)">{t(labelKey)}</Text>
      <BlockItemGrid itemSlots={data.itemSlots} slotIndices={indices} testId={testId} />
    </Stack>
  );

  return (
    <Stack gap="sm" data-testid="rocket-launch-pad-view">
      {group(L.ui.blockInventory.rocketMaterials, detail.materialSlots, "rocket-launch-pad-material-slots")}
      {group(L.ui.blockInventory.rocketPayloads, detail.payloadSlots, "rocket-launch-pad-payload-slots")}
      <Text size="sm" c="var(--text-high-contrast)" data-testid="rocket-launch-pad-status">{t(rocketStatusKey(detail))}</Text>
      <GaugeBar value={detail.progress} testId="rocket-launch-pad-progress" />
      {group(L.ui.blockInventory.rocketOutputs, detail.outputSlots, "rocket-launch-pad-output-slots")}
    </Stack>
  );
}
