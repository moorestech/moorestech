import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { FluidIcon } from "@/shared/ui";
import LackHighlightText from "../LackHighlightText";
import PowerRateText from "../PowerRateText";
import { machineStateDisplay } from "../detailLogic";
import { L, useI18n } from "@/shared/i18n";
import styles from "./pumpSection.module.css";

// ポンプ: 動力行/公称生成速度/鉱脈警告
// Pump: power row / nominal rates / vein warning
export default function PumpSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.pump) return null;
  // 動力行の有無は種別が決める。electricの有無を種別の代用にすると歯車ポンプでも動力行が生える
  // The kind decides whether a power row exists; standing in electric's presence would grow one on the gear pump too
  const electric = data.pump.kind === "electric" ? data.pump.electric : null;
  // electricとstateを1つの値へ束ね、判定源を1箇所に絞る（恒真の二重ガード回避）
  // Bundle electric and state into one value so presence has a single source of truth (avoids a tautological double guard)
  const electricDisplay = electric ? { power: electric, state: machineStateDisplay(electric.currentState) } : null;
  // 汲み上げ対象の有無が流体行と警告行を排他に分ける（姉妹セクションと同じくJSX直判定）
  // Whether the pump has targets splits the fluid rows from the warning row (a direct JSX check, as in the sibling sections)
  const hasTargets = data.pump.pumpingFluids.length > 0;
  return (
    <Stack gap="xs" data-testid="pump-section">
      {electricDisplay ? (
        <>
          <LackHighlightText insufficient={electricDisplay.state.insufficient} size="sm" testId="pump-state-label">{t(electricDisplay.state.labelKey)}</LackHighlightText>
          {electricDisplay.state.showPowerRate && <PowerRateText currentPower={electricDisplay.power.currentPower} requestPower={electricDisplay.power.requestPower} testId="pump-power-rate" />}
        </>
      ) : null}
      {hasTargets ? (
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
      ) : (
        <LackHighlightText insufficient size="sm" testId="pump-no-vein">{t(L.ui.blockInventory.pumpNoVein)}</LackHighlightText>
      )}
    </Stack>
  );
}
