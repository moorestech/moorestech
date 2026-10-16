import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import LackHighlightText from "./LackHighlightText";
import { gearToElectricStateDisplay } from "./detailLogic";
import { HoverTooltip } from "@/shared/ui";
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
      <HoverTooltip label={t(L.ui.blockInventory.gearToElectricMeasurementTiming)}>
        <div>
          <LackHighlightText insufficient={display.insufficient} size="sm" testId="gear-to-electric-state">{t(display.labelKey)}</LackHighlightText>
        </div>
      </HoverTooltip>
      <Text size="sm" c="var(--text-default)" data-testid="gear-to-electric-supplied-power">
        {t(L.ui.blockInventory.gearToElectricSuppliedPower, {
          current: formatPower(detail.suppliedPower),
          max: formatPower(detail.maxGeneratedPower),
        })}
      </Text>
    </Stack>
  );
}

// 正の微小値だけ有効桁を残し、供給と容量をゼロ表示にしない
// Preserve significant digits only for tiny positive values so neither supply nor capacity reads as zero
function formatPower(power: number): string {
  const rounded = power.toFixed(0);
  return power > 0 && rounded === "0" ? power.toPrecision(2) : rounded;
}
