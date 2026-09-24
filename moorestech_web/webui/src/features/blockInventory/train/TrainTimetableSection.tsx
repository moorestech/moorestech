// 時刻表タブ。ローカルdraftを編集し「適用」で丸ごと送る。自動運転は即送信
// Timetable tab: edit a local draft and send the whole list on Apply; auto-run is sent immediately
import { useState } from "react";
import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { TrainTimetableData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch, PanelActionButton } from "@/shared/ui";
import TrainTimetableStopList from "./TrainTimetableStopList";
import { addStop, canEnableAutoRun, moveStop, removeStop, stationKey, stationLabel, toReplacePayload, sameStopOrder, type TimetableDraft } from "./timetableEditLogic";
import styles from "./style.module.css";

export default function TrainTimetableSection({ timetable }: { timetable: TrainTimetableData }) {
  const { t } = useI18n();
  // 適用前に閉じたら破棄される（コンポーネントのアンマウントで消える）
  // Discarded when closed before Apply (state dies with the component)
  const [draft, setDraft] = useState<TimetableDraft>({ stops: timetable.stops });
  const [previousStops, setPreviousStops] = useState(timetable.stops);
  // 未編集なら最新snapshotへ追従し、編集中の並びは保持する
  // Follow new snapshots while clean, preserving an actively edited order
  if (previousStops !== timetable.stops) {
    setPreviousStops(timetable.stops);
    if (sameStopOrder(draft.stops, previousStops)) setDraft({ stops: timetable.stops });
  }
  const matchesServer = sameStopOrder(draft.stops, timetable.stops);
  const autoRunOptions = [
    { value: "on", label: t(L.ui.blockInventory.timetableAutoRunOn), testId: "train-timetable-auto-run-on", disabled: !canEnableAutoRun(timetable.stops) },
    { value: "off", label: t(L.ui.blockInventory.timetableAutoRunOff), testId: "train-timetable-auto-run-off" },
  ];
  return (
    <Stack gap="xs" data-testid="train-timetable-section">
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
      <Text size="sm">{t(L.ui.blockInventory.timetableStops)}</Text>
      <TrainTimetableStopList
        stops={draft.stops}
        currentIndex={matchesServer ? timetable.currentIndex : -1}
        onMove={(i, d) => setDraft((prev) => moveStop(prev, i, d))}
        onRemove={(i) => setDraft((prev) => removeStop(prev, i))}
      />
      <Text size="sm">{t(L.ui.blockInventory.timetableStations)}</Text>
      <div className={styles.list} data-testid="train-timetable-stations">
        {timetable.stations.length === 0 && <Text size="sm">{t(L.ui.blockInventory.timetableStationsEmpty)}</Text>}
        {timetable.stations.map((station) => (
          <div className={styles.row} key={stationKey(station.position)}>
            <span>{stationLabel(t, station)}</span>
            <PanelActionButton
              testId={`train-timetable-station-${stationKey(station.position).replace(/,/g, "_")}-add`}
              onClick={() => setDraft((prev) => addStop(prev, station))}
            >
              {t(L.ui.blockInventory.timetableAdd)}
            </PanelActionButton>
          </div>
        ))}
      </div>
      <PanelActionButton testId="train-timetable-apply" onClick={() => { void dispatchAction("train_timetable.replace", toReplacePayload(draft)); }}>
        {t(L.ui.blockInventory.timetableApply)}
      </PanelActionButton>
    </Stack>
  );
}
