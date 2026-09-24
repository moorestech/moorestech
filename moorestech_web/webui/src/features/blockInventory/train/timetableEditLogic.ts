// 時刻表タブのローカル編集。サーバーへは「適用」で丸ごと送る
// Local editing for the timetable tab; the whole list is sent on "Apply"
import type { TrainTimetableStation } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";

export type TimetableDraft = { stops: TrainTimetableStation[] };
export type StationKey = string;
export type StationPosition = TrainTimetableStation["position"];

export function stationKey(position: StationPosition): StationKey {
  return `${position.x},${position.y},${position.z}`;
}

export function addStop(draft: TimetableDraft, station: TrainTimetableStation): TimetableDraft {
  return { stops: [...draft.stops, station] };
}

export function removeStop(draft: TimetableDraft, index: number): TimetableDraft {
  if (!Number.isInteger(index) || index < 0 || index >= draft.stops.length) {
    console.warn("[TrainTimetable] remove ignored: invalid stop index", index);
    return draft;
  }
  return { stops: draft.stops.filter((_, i) => i !== index) };
}

export function moveStop(draft: TimetableDraft, index: number, delta: -1 | 1): TimetableDraft {
  const target = index + delta;
  if (!Number.isInteger(index) || index < 0 || index >= draft.stops.length || target < 0 || target >= draft.stops.length) {
    console.warn("[TrainTimetable] move ignored: stop index outside timetable", index, delta);
    return draft;
  }
  const stops = [...draft.stops];
  [stops[index], stops[target]] = [stops[target], stops[index]];
  return { stops };
}

// 空の時刻表ではONにしない（サーバーは受理して即OFFに戻すため、UI側で塞ぐ裁定）
// Never enable on an empty timetable (the server accepts then immediately turns it off, so the UI blocks it)
export function canEnableAutoRun(stops: readonly TrainTimetableStation[]): boolean {
  return stops.length > 0;
}

export type Translator = ReturnType<typeof useI18n>["t"];

export function stationLabel(t: Translator, station: TrainTimetableStation): string {
  const name = station.name.length > 0 ? station.name : t(L.ui.blockInventory.timetableUnnamedStation);
  return t(L.ui.blockInventory.timetableStationLabel, { name, x: station.position.x, y: station.position.y, z: station.position.z });
}

export function toReplacePayload(draft: TimetableDraft): { stations: StationPosition[] } {
  return { stations: draft.stops.map((s) => ({ x: s.position.x, y: s.position.y, z: s.position.z })) };
}

// 順序が同じときだけサーバーの行番号を編集リストへ適用する
// Apply the server row index to the draft only when both orders match
export function sameStopOrder(left: readonly TrainTimetableStation[], right: readonly TrainTimetableStation[]): boolean {
  return left.length === right.length && left.every((stop, i) => stationKey(stop.position) === stationKey(right[i].position));
}
