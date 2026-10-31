import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { GaugeBar } from "@/shared/ui";
import { BlockItemGridGroup } from "../BlockItemGrid";
import { rocketStatusKey } from "../details/rocket/rocketLaunchPadLogic";

export default function RocketLaunchPadInventory({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.blockType === "RocketLaunchPad" ? data.rocketLaunchPad : undefined;
  // 詳細未着は取得待ちとして伝える
  // Show pending details as loading
  if (!detail) return <Text size="sm" c="var(--text-muted)" data-testid="rocket-launch-pad-loading">{t(L.ui.blockInventory.detailLoading)}</Text>;


  return (
    <Stack gap="sm" data-testid="rocket-launch-pad-view">
      <BlockItemGridGroup itemSlots={data.itemSlots} labelKey={L.ui.blockInventory.rocketMaterials} slotIndices={detail.materialSlots} testId="rocket-launch-pad-material-slots" />
      <BlockItemGridGroup itemSlots={data.itemSlots} labelKey={L.ui.blockInventory.rocketPayloads} slotIndices={detail.payloadSlots} testId="rocket-launch-pad-payload-slots" />
      <Text size="sm" c="var(--text-high-contrast)" data-testid="rocket-launch-pad-status">{t(rocketStatusKey(detail))}</Text>
      <GaugeBar value={detail.progress} testId="rocket-launch-pad-progress" />
      <BlockItemGridGroup itemSlots={data.itemSlots} labelKey={L.ui.blockInventory.rocketOutputs} slotIndices={detail.outputSlots} testId="rocket-launch-pad-output-slots" />
    </Stack>
  );
}
