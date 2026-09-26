// 時刻表タブ。draft編集→適用で送信、自動運転は即送信
// Timetable tab: edit a draft, send on Apply; auto-run sends immediately
import { useRef, useState } from "react";
import { Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { TrainTimetableData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch, PanelActionButton } from "@/shared/ui";
import TrainTimetableStopList from "./TrainTimetableStopList";
import { addStop, canEnableAutoRun, moveStop, removeStop, stationKey, stationLabel, toReplacePayload, sameStopOrder } from "./timetableEditLogic";
import styles from "./style.module.css";

export default function TrainTimetableSection({ timetable }: { timetable: TrainTimetableData }) {
  const { t } = useI18n();
  // 適用前に閉じるとdraftは破棄（アンマウントで消える）
  // The draft is discarded if closed before Apply (dies on unmount)
  const [draft, setDraft] = useState(timetable.stops);
  const [previousStops, setPreviousStops] = useState(timetable.stops);
  // 未編集は最新snapshotに追従、編集中は並び保持
  // Follow the latest snapshot while clean; keep order while editing
  if (previousStops !== timetable.stops) {
    setPreviousStops(timetable.stops);
    if (sameStopOrder(draft, previousStops)) setDraft(timetable.stops);
  }
  const matchesServer = sameStopOrder(draft, timetable.stops);
  // 送信の待ち時間中に届いた権威を掴むための最新スナップショット
  // Holds the latest snapshot so a send that resolves late reverts to the authority that arrived meanwhile
  const latestStops = useRef(timetable.stops);
  latestStops.current = timetable.stops;
  // 非受理・到達不能なら編集を捨てて権威へ戻す（dirtyのまま二度と再同期されないのを防ぐ）
  // A rejected or unreachable send drops the edits and returns to authority (never stays dirty forever)
  async function applyDraft() {
    const ok = await dispatchAction("train_timetable.replace", toReplacePayload(draft));
    if (ok) return;
    console.warn("[TrainTimetable] replace not accepted: reverting the draft to the server timetable");
    setDraft(latestStops.current);
  }
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
        stops={draft}
        currentIndex={matchesServer ? timetable.currentIndex : null}
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
      <PanelActionButton testId="train-timetable-apply" onClick={() => { void applyDraft(); }}>
        {t(L.ui.blockInventory.timetableApply)}
      </PanelActionButton>
    </Stack>
  );
}
