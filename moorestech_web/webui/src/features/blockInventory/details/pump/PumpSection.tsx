import { Group, Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { FluidIcon } from "@/shared/ui";
import LackHighlightText from "../LackHighlightText";
import MachineStateRow from "../rows/MachineStateRow";
import PerMinuteRateRow from "../rows/PerMinuteRateRow";
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
  // 汲み上げ対象の有無が流体行と警告行を排他に分ける（姉妹セクションと同じくJSX直判定）
  // Whether the pump has targets splits the fluid rows from the warning row (a direct JSX check, as in the sibling sections)
  const hasTargets = data.pump.pumpingFluids.length > 0;
  return (
    <Stack gap="xs" data-testid="pump-section">
      {electric ? (
        <MachineStateRow currentState={electric.currentState} currentPower={electric.currentPower} requestPower={electric.requestPower} stateTestId="pump-state-label" powerRateTestId="pump-power-rate" />
      ) : null}
      {hasTargets ? (
        <Group gap="xs" data-testid="pump-pumping-fluids">
          {data.pump.pumpingFluids.map((fluid, i) => (
            <PerMinuteRateRow key={`${fluid.fluidId}-${i}`} amountPerMinute={fluid.amountPerMinute}>
              <FluidIcon fluidGuid={fluid.fluidGuid} className={styles.icon} />
            </PerMinuteRateRow>
          ))}
        </Group>
      ) : (
        <LackHighlightText insufficient size="sm" testId="pump-no-vein">{t(L.ui.blockInventory.pumpNoVein)}</LackHighlightText>
      )}
    </Stack>
  );
}
