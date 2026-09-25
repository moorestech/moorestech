using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public class TrainStationPositionDto
    {
        public int X;
        public int Y;
        public int Z;
    }

    public class TrainTimetableStationDto
    {
        public TrainStationPositionDto Position;
        public string Name;
    }

    // 停車駅は入線する端（"front"/"back"）を持つ
    // A stop carries the side the train arrives at ("front"/"back")
    public class TrainTimetableStopDto
    {
        public TrainStationPositionDto Position;
        public string Name;
        public string Side;
    }

    // 列車1編成の時刻表と自動運転状態、選べる駅の一覧
    // One train's timetable, auto-run state, and the selectable station list
    public class TrainTimetableDto
    {
        public string TrainUnitId;
        public bool IsAutoRun;
        public int CurrentIndex;
        public List<TrainTimetableStopDto> Stops;
        public List<TrainTimetableStationDto> Stations;
    }

    public class TrainStationDetailDto
    {
        public string Name;
    }
}
