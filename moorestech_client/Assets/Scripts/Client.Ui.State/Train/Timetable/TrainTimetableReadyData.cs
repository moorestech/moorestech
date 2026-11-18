using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Train.Logic;
using Game.Train.Unit;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// サーバーから届いた時刻表と、追加先の駅一覧
    /// The timetable published by the server and the stations it can add
    /// </summary>
    public sealed class TrainTimetableReadyData
    {
        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public bool IsAutoRun { get; }
        public int CurrentIndex { get; }
        public IReadOnlyList<TimetableEditStop> Stops { get; }
        public IReadOnlyList<TrainTimetableStation> Stations { get; }

        public TrainTimetableReadyData(TrainUnitInstanceId trainUnitInstanceId, bool isAutoRun, int currentIndex, IReadOnlyList<TimetableEditStop> stops, IReadOnlyList<TrainTimetableStation> stations)
        {
            TrainUnitInstanceId = trainUnitInstanceId;
            IsAutoRun = isAutoRun;
            CurrentIndex = currentIndex;
            Stops = stops;
            Stations = stations;
        }

        // 停車駅は座標・端・名前、駅一覧は座標・名前で比べる（値の構造体の既定の Equals。前例 CursorTooltipViewModel）
        // Stops compare position, side and name; stations compare position and name (struct default Equals; precedent CursorTooltipViewModel)
        public bool HasSameContent(TrainTimetableReadyData other)
        {
            return TrainUnitInstanceId.Equals(other.TrainUnitInstanceId) && IsAutoRun == other.IsAutoRun && CurrentIndex == other.CurrentIndex
                   && Stops.SequenceEqual(other.Stops) && Stations.SequenceEqual(other.Stations);
        }
    }
}
