// 時刻表タブのローカル編集。適用で丸ごと送信
// Local editing for the timetable tab; sent whole on Apply
import type { ActionPayloads, TrainTimetableStation, TrainTimetableStop } from "@/bridge";
import { L, type InterpolationValues, type TranslationKey } from "@/shared/i18n";

type StationPosition = TrainTimetableStation["position"];

// UIは入線方向を固定し、新しい停車駅は常に後端側へ着ける（裁定 2026-09-25）
// The UI fixes the arrival direction; new stops always use the back side (ruling 2026-09-25)
const UI_FIXED_STOP_SIDE = "back" as const;

export function stationKey(position: StationPosition): string {
  return `${position.x},${position.y},${position.z}`;
}

export function addStop(stops: readonly TrainTimetableStop[], station: TrainTimetableStation): TrainTimetableStop[] {
  return [...stops, { ...station, side: UI_FIXED_STOP_SIDE }];
}

export function removeStop(stops: TrainTimetableStop[], index: number): TrainTimetableStop[] {
  if (!Number.isInteger(index) || index < 0 || index >= stops.length) {
    console.warn("[TrainTimetable] remove ignored: invalid stop index", index);
    return stops;
  }
  return stops.filter((_, i) => i !== index);
}

export function moveStop(stops: TrainTimetableStop[], index: number, delta: -1 | 1): TrainTimetableStop[] {
  const target = index + delta;
  if (!Number.isInteger(index) || index < 0 || index >= stops.length || target < 0 || target >= stops.length) {
    console.warn("[TrainTimetable] move ignored: stop index outside timetable", index, delta);
    return stops;
  }
  const moved = [...stops];
  [moved[index], moved[target]] = [moved[target], moved[index]];
  return moved;
}

// 空の時刻表ではONにしない（サーバーは受理して即OFFに戻すため、UI側で塞ぐ裁定）
// Never enable on an empty timetable (the server accepts then immediately turns it off, so the UI blocks it)
export function canEnableAutoRun(stops: readonly TrainTimetableStop[]): boolean {
  return stops.length > 0;
}

export function stationLabel(
  t: (key: TranslationKey, values?: InterpolationValues) => string,
  station: TrainTimetableStation,
): string {
  const name = station.name.length > 0 ? station.name : t(L.ui.blockInventory.timetableUnnamedStation);
  return t(L.ui.blockInventory.timetableStationLabel, { name, x: station.position.x, y: station.position.y, z: station.position.z });
}

export function toReplacePayload(stops: readonly TrainTimetableStop[]): ActionPayloads["train_timetable.replace"] {
  return { stops: stops.map((s) => ({ x: s.position.x, y: s.position.y, z: s.position.z, side: s.side })) };
}

// 順序が同じときだけサーバーの行番号を編集リストへ適用する。端の違いも別行として扱う
// Apply the server row index to the draft only when both orders match; a differing side counts as a different row
export function sameStopOrder(left: readonly TrainTimetableStop[], right: readonly TrainTimetableStop[]): boolean {
  return left.length === right.length && left.every((stop, i) => stationKey(stop.position) === stationKey(right[i].position) && stop.side === right[i].side);
}
