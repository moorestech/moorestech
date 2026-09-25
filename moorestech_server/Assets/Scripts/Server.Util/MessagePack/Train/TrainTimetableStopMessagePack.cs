using System;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class TrainTimetableStopMessagePack
    {
        [Key(0)] public Vector3IntMessagePack StationPosition { get; set; }
        [Key(1)] public StationNodeSide Side { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainTimetableStopMessagePack() { }

        public TrainTimetableStopMessagePack(TrainTimetableStop stop) : this(stop.StationPosition, stop.Side) { }

        public TrainTimetableStopMessagePack(Vector3Int stationPosition, StationNodeSide side)
        {
            StationPosition = new Vector3IntMessagePack(stationPosition);
            Side = side;
        }

        public TrainTimetableStop ToModel()
        {
            return new TrainTimetableStop(StationPosition.Vector3Int, Side);
        }
    }
}
