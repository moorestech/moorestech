import { Radio, Slider, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";

const labels = {
  fulfillment: "充足率",
  consumedPower: "消費電力",
};

export default function ElectricToGearInventory({ data }: { data: BlockInventoryOpen }) {
  const detail = data.electricToGear;
  if (!detail) return null;

  const selectMode = (value: string) => {
    void dispatchAction("electric_to_gear.set_output_mode", { modeIndex: Number(value) });
  };

  return (
    <Stack gap="sm" data-testid="electric-to-gear-view">
      <Radio.Group value={String(detail.selectedIndex)} onChange={selectMode}>
        <Stack gap="xs">
          {detail.outputModes.map((mode, index) => (
            // testidはラベル文字列を含む行ラッパーに付ける（Radioのinput自体はテキストを持たない）
            // Attach the testid to the row wrapper that contains the label text (the radio input itself has no text)
            <div key={index} data-testid={`electric-to-gear-mode-${index}`}>
              <Radio
                value={String(index)}
                label={`${mode.rpm} rpm / ${mode.torque} trq / ${mode.requiredPower} W`}
              />
            </div>
          ))}
        </Stack>
      </Radio.Group>
      <Text size="sm">{labels.fulfillment}</Text>
      <Slider value={detail.fulfillmentRate * 100} min={0} max={100} disabled data-testid="electric-to-gear-fulfillment" />
      <Text data-testid="electric-to-gear-consumed-power">
        {labels.consumedPower}: {detail.consumedElectricPower.toFixed(0)} W
      </Text>
    </Stack>
  );
}
