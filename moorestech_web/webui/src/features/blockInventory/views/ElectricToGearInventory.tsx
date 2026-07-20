import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { GaugeBar, ModeSwitch } from "@/shared/ui";

const FULFILLMENT_KEY = "充足率";
const CONSUMED_POWER_KEY = "消費電力: {power} W";
const OUTPUT_MODE_KEY = "{rpm} rpm / {torque} trq / {power} W";
const OUTPUT_MODE_TEST_ID_PREFIX = "electric-to-gear-mode-";

export default function ElectricToGearInventory({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  const detail = data.electricToGear;
  if (!detail) return null;
  const fulfillmentLabel = t(FULFILLMENT_KEY);
  const consumedPowerLabel = t(CONSUMED_POWER_KEY, {
    power: detail.consumedElectricPower.toFixed(0),
  });
  const modeOptions = detail.outputModes.map((mode, index) => {
    const modeLabel = t(OUTPUT_MODE_KEY, {
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
