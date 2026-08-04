import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen, TrainPlatformMode } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

const TRAIN_PLATFORM_SECTION_TEST_ID = "train-platform-section";
const TRAIN_PLATFORM_MODE_TEST_ID = "train-platform-mode";
const TRAIN_PLATFORM_CAPACITY_TEST_ID = "train-platform-fluid-capacity";
const LOAD_MODE = "loadToTrain";
const UNLOAD_MODE = "unloadToPlatform";

export default function TrainPlatformSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.trainPlatform;
  if (!detail) return null;
  const modeOptions = [
    { value: LOAD_MODE, label: t(L.ui.blockInventory.trainPlatformLoad) },
    { value: UNLOAD_MODE, label: t(L.ui.blockInventory.trainPlatformUnload) },
  ];
  const capacityText = detail.fluidCapacity === undefined
    ? null
    : t(L.ui.blockInventory.trainPlatformCapacity, { capacity: detail.fluidCapacity });
  const capacityElement = capacityText === null
    ? null
    : (
      <Text size="sm" data-testid={TRAIN_PLATFORM_CAPACITY_TEST_ID}>
        {capacityText}
      </Text>
    );

  const setMode = (mode: string) => {
    void dispatchAction("train_platform.set_transfer_mode", {
      mode: mode as TrainPlatformMode,
    });
  };

  return (
    <Stack gap="xs" data-testid={TRAIN_PLATFORM_SECTION_TEST_ID}>
      <ModeSwitch
        testId={TRAIN_PLATFORM_MODE_TEST_ID}
        value={detail.mode}
        onChange={setMode}
        options={modeOptions}
      />
      {capacityElement}
    </Stack>
  );
}
