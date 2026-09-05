import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen, PumpDetailData } from "@/bridge";
import { FluidIcon } from "@/shared/ui";
import LackHighlightText from "./LackHighlightText";
import PowerRateText from "./PowerRateText";
import { machineStateDisplay } from "./detailLogic";
import { L, useI18n } from "@/shared/i18n";
import styles from "./pumpSection.module.css";

export type PumpSectionDisplay = { showNoVein: boolean; showPumpingFluids: boolean };

// 汲み上げ対象の有無だけで警告行と流体行を排他に出し分ける（ADR 0051）
// Whether the pump has targets alone decides between the warning row and the fluid rows (ADR 0051)
export function pumpSectionDisplay(pump: Pick<PumpDetailData, "pumpingFluids">): PumpSectionDisplay {
  const hasTargets = pump.pumpingFluids.length > 0;
  return { showNoVein: !hasTargets, showPumpingFluids: hasTargets };
}

// ポンプ: 動力行（油井のみ。歯車ポンプは GearSection が担う）+ 公称生成速度 + 鉱脈警告（MinerSection 準拠）
// Pump: power row (electric pump only; GearSection covers the gear pump), nominal rates, and the vein warning (mirrors MinerSection)
export default function PumpSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.pump) return null;
  const display = pumpSectionDisplay(data.pump);
  const electric = data.pump.electric;
  const stateDisplay = electric ? machineStateDisplay(electric.currentState) : null;
  return (
    <Stack gap="xs" data-testid="pump-section">
      {electric && stateDisplay ? (
        <>
          <LackHighlightText insufficient={stateDisplay.insufficient} size="sm" testId="pump-state-label">{t(stateDisplay.labelKey)}</LackHighlightText>
          {stateDisplay.showPowerRate && <PowerRateText currentPower={electric.currentPower} requestPower={electric.requestPower} testId="pump-power-rate" />}
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
