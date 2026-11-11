using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Train.Logic;
using Client.Ui.State.Train.Timetable;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表の区画に描く値。現在位置は表示中の並びがサーバーと同じときだけ当てる（Web StopList と同じ）
    /// Values drawn by the timetable section; the current row applies only when the displayed order matches the server (same as Web StopList)
    /// </summary>
    public sealed class TrainTimetableSectionViewModel
    {
        public IReadOnlyList<TimetableEditStop> Stops { get; }
        public int? CurrentIndex { get; }
        public IReadOnlyList<TrainTimetableStation> Stations { get; }
        public bool IsPending { get; }
        public bool IsAutoRun { get; }
        public bool CanEnableAutoRun { get; }

        private TrainTimetableSectionViewModel(IReadOnlyList<TimetableEditStop> stops, int? currentIndex, IReadOnlyList<TrainTimetableStation> stations, bool isPending, bool isAutoRun, bool canEnableAutoRun)
        {
            Stops = stops;
            CurrentIndex = currentIndex;
            Stations = stations;
            IsPending = isPending;
            IsAutoRun = isAutoRun;
            CanEnableAutoRun = canEnableAutoRun;
        }

        public static TrainTimetableSectionViewModel Build(TrainTimetableReadyData server, IReadOnlyList<TimetableEditStop> displayedStops, bool isPending)
        {
            // 自動運転の可否は楽観表示でなくサーバーの停車駅で決める（追加が確定するまで ON にできない）
            // Auto-run availability follows the server stops, not the optimistic list, so ON waits until an add is authoritative
            int? currentIndex = TimetableEditLogic.SameStopOrder(displayedStops, server.Stops) ? server.CurrentIndex : null;
            return new TrainTimetableSectionViewModel(displayedStops, currentIndex, server.Stations, isPending, server.IsAutoRun, TimetableEditLogic.CanEnableAutoRun(server.Stops));
        }

        // 同じ中身なら区画を描き直さない（貨車の中身の更新だけで行を作り直さない。R-5）
        // Same contents skip the redraw so a car contents update alone never rebuilds rows (R-5)
        public bool HasSameContent(TrainTimetableSectionViewModel other)
        {
            return CurrentIndex == other.CurrentIndex && IsPending == other.IsPending && IsAutoRun == other.IsAutoRun && CanEnableAutoRun == other.CanEnableAutoRun
                   && Stops.SequenceEqual(other.Stops) && Stations.SequenceEqual(other.Stations);
        }
    }
}
