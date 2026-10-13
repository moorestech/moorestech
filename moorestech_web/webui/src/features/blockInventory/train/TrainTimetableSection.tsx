// 駅の編集と自動運転を操作ごとに送信する時刻表タブ
// Timetable tab sending stop edits and auto-run changes immediately
import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { TrainTimetableData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { FadeRule, GamePanel, ModeSwitch, PanelActionButton } from "@/shared/ui";
import TrainTimetableStopList from "./TrainTimetableStopList";
import { addStop, canEnableAutoRun, moveStop, removeStop, stationKey, stationLabel, sameStopOrder } from "./timetableEditLogic";
import styles from "./style.module.css";
import { useImmediateTimetable } from "./useImmediateTimetable";

export default function TrainTimetableSection({ timetable }: { timetable: TrainTimetableData }) {
  const { t } = useI18n();
  const { stops, isPending, replace } = useImmediateTimetable(timetable.stops);
  const matchesServer = sameStopOrder(stops, timetable.stops);
  const autoRunOptions = [
    { value: "on", label: t(L.ui.blockInventory.timetableAutoRunOn), testId: "train-timetable-auto-run-on", disabled: isPending || !canEnableAutoRun(timetable.stops) },
    { value: "off", label: t(L.ui.blockInventory.timetableAutoRunOff), testId: "train-timetable-auto-run-off", disabled: isPending },
  ];
  return (
    <Stack gap="xs" data-testid="train-timetable-section" aria-busy={isPending}>
      <div className={styles.row}>
        <Text size="sm">{t(L.ui.blockInventory.timetableAutoRun)}</Text>
        <ModeSwitch
          testId="train-timetable-auto-run"
          value={timetable.isAutoRun ? "on" : "off"}
          options={autoRunOptions}
          onChange={(v) => { void dispatchAction("train_timetable.set_auto_run", { enabled: v === "on" }); }}
        />
      </div>
      {!canEnableAutoRun(timetable.stops) && <Text size="sm">{t(L.ui.blockInventory.timetableAutoRunUnavailable)}</Text>}
      <GamePanel variant="craft">
        <div className={`${styles.row} ${styles.sectionHeading}`}>
          <Text size="sm">{t(L.ui.blockInventory.timetableStops)}</Text>
        </div>
        <FadeRule />
        <TrainTimetableStopList
          stops={stops}
          disabled={isPending}
          currentIndex={matchesServer ? timetable.currentIndex : null}
          onMove={(i, d) => void replace(moveStop(stops, i, d))}
          onRemove={(i) => void replace(removeStop(stops, i))}
        />
      </GamePanel>
      <GamePanel variant="craft">
        <Text className={styles.sectionHeading} size="sm">{t(L.ui.blockInventory.timetableStations)}</Text>
        <FadeRule />
        <div className={styles.list} data-testid="train-timetable-stations">
          {timetable.stations.length === 0 && <Text size="sm">{t(L.ui.blockInventory.timetableStationsEmpty)}</Text>}
          {timetable.stations.map((station) => (
            <div className={styles.row} key={stationKey(station.position)}>
              <span>{stationLabel(t, station)}</span>
              <PanelActionButton
                testId={`train-timetable-station-${stationKey(station.position).replace(/,/g, "_")}-add`}
                disabled={isPending}
                onClick={() => { void replace(addStop(stops, station)); }}
              >
                {t(L.ui.blockInventory.timetableAdd)}
              </PanelActionButton>
            </div>
          ))}
        </div>
      </GamePanel>
    </Stack>
  );
}
