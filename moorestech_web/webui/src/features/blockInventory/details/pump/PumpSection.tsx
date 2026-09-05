import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { FluidIcon } from "@/shared/ui";
import LackHighlightText from "../LackHighlightText";
import PowerRateText from "../PowerRateText";
import { machineStateDisplay, pumpSectionDisplay } from "../detailLogic";
import { L, useI18n } from "@/shared/i18n";
import styles from "./pumpSection.module.css";

// ポンプ: 動力行/公称生成速度/鉱脈警告
// Pump: power row / nominal rates / vein warning
export default function PumpSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.pump) return null;
  const display = pumpSectionDisplay(data.pump);
  const electric = data.pump.electric;
  // electricとstateを1つの値へ束ね、判定源を1箇所に絞る（恒真の二重ガード回避）
  // Bundle electric and state into one value so presence has a single source of truth (avoids a tautological double guard)
  const electricDisplay = electric ? { power: electric, state: machineStateDisplay(electric.currentState) } : null;
  return (
    <Stack gap="xs" data-testid="pump-section">
      {electricDisplay ? (
        <>
          <LackHighlightText insufficient={electricDisplay.state.insufficient} size="sm" testId="pump-state-label">{t(electricDisplay.state.labelKey)}</LackHighlightText>
          {electricDisplay.state.showPowerRate && <PowerRateText currentPower={electricDisplay.power.currentPower} requestPower={electricDisplay.power.requestPower} testId="pump-power-rate" />}
        </>
      ) : null}
      {display.showPumpingFluids ? (
        <Group gap="xs" data-testid="pump-pumping-fluids">
          {data.pump.pumpingFluids.map((fluid, i) => (
            <Group key={`${fluid.fluidId}-${i}`} gap={4}>
              <FluidIcon fluidGuid={fluid.fluidGuid} className={styles.icon} />
              <Text size="xs" c="var(--text-default)">
                {t(L.ui.blockInventory.itemsPerMinute, { itemsPerMinute: fluid.amountPerMinute.toFixed(1) })}
              </Text>
            </Group>
          ))}
        </Group>
      ) : null}
      {display.showNoVein ? (
        <LackHighlightText insufficient size="sm" testId="pump-no-vein">{t(L.ui.blockInventory.pumpNoVein)}</LackHighlightText>
      ) : null}
    </Stack>
  );
}
