import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import LackHighlightText from "./LackHighlightText";
import { gearToElectricStateDisplay } from "./detailLogic";
import { L, useI18n } from "@/shared/i18n";

// 発電状態とこの1台が電力網へ渡した量を表示する
// Show generation state and the amount this unit handed to the network
export default function GearToElectricSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gearToElectric) return null;
  const detail = data.gearToElectric;
  const display = gearToElectricStateDisplay(detail.generationState);
  return (
    <Stack gap={2} data-testid="gear-to-electric-section">
      <LackHighlightText insufficient={display.insufficient} size="sm" testId="gear-to-electric-state">{t(display.labelKey)}</LackHighlightText>
      <Text size="sm" c="var(--text-default)" data-testid="gear-to-electric-supplied-power">
        {t(L.ui.blockInventory.gearToElectricSuppliedPower, {
          current: detail.suppliedPower.toFixed(0),
          max: detail.maxGeneratedPower.toFixed(0),
        })}
      </Text>
    </Stack>
  );
}
