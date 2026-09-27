// 停車駅リスト。現在行はdata-current、↑↓×で並替/削除
// Stop list; current row has data-current, up/down/remove controls
import { Text } from "@mantine/core";
import type { TrainTimetableStop } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import { stationLabel } from "./timetableEditLogic";
import styles from "./style.module.css";

type Props = {
  stops: TrainTimetableStop[];
  currentIndex: number | null;
  onMove: (index: number, delta: -1 | 1) => void;
  onRemove: (index: number) => void;
};

export default function TrainTimetableStopList({ stops, currentIndex, onMove, onRemove }: Props) {
  const { t } = useI18n();
  if (stops.length === 0) return <Text size="sm" data-testid="train-timetable-stops">{t(L.ui.blockInventory.timetableStopsEmpty)}</Text>;
  return (
    <ol className={styles.list} data-testid="train-timetable-stops">
      {stops.map((stop, i) => (
        <li className={styles.row} key={`${i}-${stop.position.x}-${stop.position.z}`} data-testid={`train-timetable-stop-${i}`} data-current={i === currentIndex ? "true" : undefined}>
          <span>{t(L.ui.blockInventory.timetableStopLabel, { index: i + 1, station: stationLabel(t, stop) })}</span>
          <span className={styles.controls}>
            <PanelActionButton testId={`train-timetable-stop-${i}-up`} onClick={() => onMove(i, -1)}>{t(L.ui.blockInventory.timetableMoveUp)}</PanelActionButton>
            <PanelActionButton testId={`train-timetable-stop-${i}-down`} onClick={() => onMove(i, 1)}>{t(L.ui.blockInventory.timetableMoveDown)}</PanelActionButton>
            <PanelActionButton testId={`train-timetable-stop-${i}-remove`} onClick={() => onRemove(i)}>{t(L.ui.blockInventory.timetableRemove)}</PanelActionButton>
          </span>
        </li>
      ))}
    </ol>
  );
}
