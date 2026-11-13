using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.Text;
using Game.Train.RailGraph;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Train.Logic
{
    /// <summary>
    /// 時刻表の各操作から即時送信用の停車駅一覧を組み立てる（Web timetableEditLogic.ts の写し）。元の一覧は変えない
    /// Builds the stop list sent immediately for each operation (port of Web timetableEditLogic.ts); the source list is never mutated
    /// </summary>
    public static class TimetableEditLogic
    {
        // UI は入線方向を固定し、新しい停車駅は常に後端側へ着ける（裁定 2026-09-25）
        // The UI fixes the arrival direction; new stops always use the back side (ruling 2026-09-25)
        private const StationNodeSide UiFixedStopSide = StationNodeSide.Back;

        public static IReadOnlyList<TimetableEditStop> AddStop(IReadOnlyList<TimetableEditStop> stops, TrainTimetableStation station)
        {
            var added = new List<TimetableEditStop>(stops) { new TimetableEditStop(station.Position, station.Name, UiFixedStopSide) };
            return added;
        }

        // 範囲外の番号は無視して同じ一覧を返す。無視した理由は警告ログへ出す
        // Out-of-range indices are ignored and the same list is returned; the reason goes to a warning log
        public static IReadOnlyList<TimetableEditStop> RemoveStop(IReadOnlyList<TimetableEditStop> stops, int index)
        {
            if (index < 0 || stops.Count <= index)
            {
                Debug.LogWarning($"[TrainTimetable] remove ignored: invalid stop index {index}");
                return stops;
            }
            var removed = new List<TimetableEditStop>(stops);
            removed.RemoveAt(index);
            return removed;
        }

        public static IReadOnlyList<TimetableEditStop> MoveStop(IReadOnlyList<TimetableEditStop> stops, int index, StopMoveDirection direction)
        {
            var target = index + (int)direction;
            if (index < 0 || stops.Count <= index || target < 0 || stops.Count <= target)
            {
                Debug.LogWarning($"[TrainTimetable] move ignored: stop index outside timetable {index} {direction}");
                return stops;
            }
            var moved = new List<TimetableEditStop>(stops);
            (moved[index], moved[target]) = (moved[target], moved[index]);
            return moved;
        }

        // 空の時刻表では ON にしない（サーバーは受理して即 OFF に戻すため UI 側で塞ぐ裁定）
        // Never enable on an empty timetable (the server accepts then turns it off at once, so the UI blocks it)
        public static bool CanEnableAutoRun(IReadOnlyList<TimetableEditStop> stops)
        {
            return 0 < stops.Count;
        }

        // 表示名は座標を必ず含め、同名の別駅を見分けられるようにする
        // Labels always carry coordinates so same-named stations stay distinguishable
        public static UiText StationLabel(TrainTimetableStation station, string unnamedStationName)
        {
            var name = 0 < station.Name.Length ? station.Name : unnamedStationName;
            return new UiText(LocalizationKeys.Ui.BlockInventory.TimetableStationLabel, new[]
            {
                new LocalizationTextParam("name", name),
                new LocalizationTextParam("x", UiNumberFormat.Integer(station.Position.x)),
                new LocalizationTextParam("y", UiNumberFormat.Integer(station.Position.y)),
                new LocalizationTextParam("z", UiNumberFormat.Integer(station.Position.z)),
            });
        }

        public static List<TimetableReplaceStop> ToReplacePayload(IReadOnlyList<TimetableEditStop> stops)
        {
            var payload = new List<TimetableReplaceStop>(stops.Count);
            foreach (var stop in stops) payload.Add(new TimetableReplaceStop(stop.Position, stop.Side));
            return payload;
        }

        // 順序が同じときだけサーバーの行番号を編集リストへ当てる。端の違いも別行、名前の違いは同じ行
        // Apply the server row index only when both orders match; a differing side is a different row, a differing name is not
        public static bool SameStopOrder(IReadOnlyList<TimetableEditStop> left, IReadOnlyList<TimetableEditStop> right)
        {
            if (left.Count != right.Count) return false;
            // 行の同一は送信形（座標と端）の等値 1 か所で決める
            // Row identity is decided once by the payload (position and side) equality
            var leftPayload = ToReplacePayload(left);
            var rightPayload = ToReplacePayload(right);
            for (var i = 0; i < leftPayload.Count; i++)
            {
                if (!leftPayload[i].Equals(rightPayload[i])) return false;
            }
            return true;
        }
    }
}
