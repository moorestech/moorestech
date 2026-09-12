import type { MachineProcessState } from "@/bridge";
import LackHighlightText from "../LackHighlightText";
import PowerRateText from "./PowerRateText";
import { machineStateDisplay } from "../detailLogic";
import { useI18n } from "@/shared/i18n";

// 稼働ラベルと充足率の組。機械・油井が同じ行を共有し、状態テーブルの変更が片側だけに効かないようにする
// The state label paired with its satisfaction rate; machines and the oil well share it so a state-table change can never land on one side only
export default function MachineStateRow({
  currentState,
  currentPower,
  requestPower,
  highlightPowerShortage,
  stateTestId,
  powerRateTestId,
}: {
  currentState: MachineProcessState;
  currentPower: number;
  requestPower: number;
  highlightPowerShortage: boolean;
  stateTestId: string;
  powerRateTestId: string;
}) {
  const { t } = useI18n();
  const display = machineStateDisplay(currentState);
  return (
    <>
      <LackHighlightText insufficient={display.insufficient} size="sm" testId={stateTestId}>{t(display.labelKey)}</LackHighlightText>
      {display.showPowerRate && <PowerRateText currentPower={currentPower} requestPower={requestPower} highlightShortage={highlightPowerShortage} testId={powerRateTestId} />}
    </>
  );
}
