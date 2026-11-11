using System.Collections.Generic;
using Client.Ui.State.Train.Logic;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 駅一覧を x → z → y で並べる（Web ホスト TrainTimetableDtoBuilder.SortStations と同じ規則。D-2）
    /// Orders stations by x, then z, then y (same rule as the Web host TrainTimetableDtoBuilder.SortStations; D-2)
    /// </summary>
    public static class TrainTimetableStationCatalog
    {
        public static List<TrainTimetableStation> Sorted(IEnumerable<TrainTimetableStation> stations)
        {
            var sorted = new List<TrainTimetableStation>(stations);
            sorted.Sort((a, b) =>
            {
                var byX = a.Position.x.CompareTo(b.Position.x);
                if (byX != 0) return byX;
                var byZ = a.Position.z.CompareTo(b.Position.z);
                return byZ != 0 ? byZ : a.Position.y.CompareTo(b.Position.y);
            });
            return sorted;
        }
    }
}
