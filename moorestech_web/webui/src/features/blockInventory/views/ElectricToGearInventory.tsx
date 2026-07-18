import { Radio, Slider, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { useI18n } from "@/shared/i18n";

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
  const modeRows = detail.outputModes.map((mode, index) => {
    const modeLabel = t(OUTPUT_MODE_KEY, {
      rpm: mode.rpm,
      torque: mode.torque,
      power: mode.requiredPower,
    });
    const testId = `${OUTPUT_MODE_TEST_ID_PREFIX}${index}`;
    return (
      // testidはラベルを含む行に付ける
      // Attach the testid to the row wrapper that contains the label text (the radio input itself has no text)
      <div key={index} data-testid={testId}>
        <Radio value={String(index)} label={modeLabel} />
      </div>
    );
  });

  const selectMode = (value: string) => {
    void dispatchAction("electric_to_gear.set_output_mode", { modeIndex: Number(value) });
  };

  return (
    <Stack gap="sm" data-testid="electric-to-gear-view">
      <Radio.Group value={String(detail.selectedIndex)} onChange={selectMode}>
        <Stack gap="xs">
          {modeRows}
        </Stack>
      </Radio.Group>
      <Text size="sm">{fulfillmentLabel}</Text>
      <Slider value={detail.fulfillmentRate * 100} min={0} max={100} disabled data-testid="electric-to-gear-fulfillment" />
      <Text data-testid="electric-to-gear-consumed-power">
        {consumedPowerLabel}
      </Text>
    </Stack>
  );
}
