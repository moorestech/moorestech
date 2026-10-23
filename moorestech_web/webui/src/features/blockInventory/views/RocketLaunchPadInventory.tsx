import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen, RocketLaunchPadData } from "@/bridge";
import { L, useI18n, type TranslationKey } from "@/shared/i18n";
import { GaugeBar } from "@/shared/ui";
import BlockItemGrid from "../BlockItemGrid";
import { rocketStatusKey } from "../details/rocket/rocketLaunchPadLogic";

type RocketViewData = Pick<BlockInventoryOpen, "itemSlots"> & {
  blockType: "RocketLaunchPad";
  rocketLaunchPad: RocketLaunchPadData | undefined;
};

export default function RocketLaunchPadInventory({ data }: { data: RocketViewData }) {
  const { t } = useI18n();
  const detail = data.rocketLaunchPad;
  // 詳細未着は取得待ちとして伝える
  // Show pending details as loading
  if (!detail) return <Text size="sm" c="var(--text-muted)" data-testid="rocket-launch-pad-loading">{t(L.ui.blockInventory.detailLoading)}</Text>;

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
