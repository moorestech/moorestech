using System;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class TrainTimetableStopMessagePack
    {
        [Key(0)] public Vector3IntMessagePack StationPosition { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainTimetableStopMessagePack() { }

        public TrainTimetableStopMessagePack(TrainTimetableStopSnapshot stop)
        {
            StationPosition = new Vector3IntMessagePack(stop.StationPosition);
        }

        public TrainTimetableStopSnapshot ToModel()
        {
            return new TrainTimetableStopSnapshot(StationPosition.Vector3Int);
        }
    }
}
