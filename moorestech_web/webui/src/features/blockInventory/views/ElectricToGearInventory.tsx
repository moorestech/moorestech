import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { GaugeBar, ModeSwitch } from "@/shared/ui";

const OUTPUT_MODE_TEST_ID_PREFIX = "electric-to-gear-mode-";

export default function ElectricToGearInventory({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.electricToGear;
  if (!detail) return null;
  const fulfillmentLabel = t(L.ui.blockInventory.electricToGearFulfillment);
  const consumedPowerLabel = t(L.ui.blockInventory.electricToGearConsumedPower, {
    power: detail.consumedElectricPower.toFixed(0),
  });
  const modeOptions = detail.outputModes.map((mode, index) => {
    const modeLabel = t(L.ui.blockInventory.electricToGearOutputMode, {
      rpm: mode.rpm,
      torque: mode.torque,
      power: mode.requiredPower,
    });
    return { value: String(index), label: modeLabel, testId: `${OUTPUT_MODE_TEST_ID_PREFIX}${index}` };
  });

  const selectMode = (value: string) => {
    void dispatchAction("electric_to_gear.set_output_mode", { modeIndex: Number(value) });
  };

  return (
    <Stack gap="sm" data-testid="electric-to-gear-view">
      <ModeSwitch orientation="vertical" value={String(detail.selectedIndex)} options={modeOptions} onChange={selectMode} />
      <Text size="sm">{fulfillmentLabel}</Text>
      <GaugeBar value={detail.fulfillmentRate} testId="electric-to-gear-fulfillment" />
      <Text data-testid="electric-to-gear-consumed-power">
        {consumedPowerLabel}
      </Text>
    </Stack>
  );
}
