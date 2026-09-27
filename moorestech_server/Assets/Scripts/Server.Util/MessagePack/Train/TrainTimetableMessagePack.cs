using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    // UI向け時刻表の通信形。tick番号は持たない
    // Wire form of the UI-facing timetable; carries no tick number
    [MessagePackObject]
    public class TrainTimetableMessagePack
    {
        [Key(0)] public TrainUnitInstanceId TrainUnitInstanceId { get; set; }
        [Key(1)] public bool IsAutoRun { get; set; }
        [Key(2)] public int CurrentIndex { get; set; }
        [Key(3)] public List<TrainTimetableStopMessagePack> Stops { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainTimetableMessagePack() { }

        public TrainTimetableMessagePack(TrainTimetableSnapshot snapshot)
        {
            TrainUnitInstanceId = snapshot.TrainUnitInstanceId;
            IsAutoRun = snapshot.IsAutoRun;
            CurrentIndex = snapshot.CurrentIndex;
            Stops = snapshot.Stops.Select(stop => new TrainTimetableStopMessagePack(stop)).ToList();
        }

        public TrainTimetableSnapshot ToModel()
        {
            return new TrainTimetableSnapshot(TrainUnitInstanceId, IsAutoRun, CurrentIndex, Stops.Select(stop => stop.ToModel()).ToList());
        }
    }
}
