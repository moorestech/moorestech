using System;
using Game.Train.Unit;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class TrainTimetableStopMessagePack
    {
        [Key(0)] public Vector3IntMessagePack StationPosition { get; set; }
        [Key(1)] public TrainTimetableStopSideWireValue Side { get; set; }
        [Key(2)] public TrainTimetableDepartureConditionWireValue DepartureCondition { get; set; }
        [Key(3)] public int WaitTicks { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainTimetableStopMessagePack() { }

        public TrainTimetableStopMessagePack(TrainTimetableStop stop)
        {
            StationPosition = new Vector3IntMessagePack(stop.StationPosition);
            Side = TrainTimetableWireMapping.ToWire(stop.Side);
            DepartureCondition = TrainTimetableWireMapping.ToWire(stop.DepartureConditionType);
            WaitTicks = stop.WaitTicks;
        }

        // サーバーが積んだ値だけが届く復路。未指定が来たら既定へ倒しつつ理由をログへ残す
        // Return path carrying server-sent values only; an unspecified value falls back to the default with a logged reason
        public TrainTimetableStop ToModel()
        {
            if (!TrainTimetableWireMapping.TryToStationNodeSide(Side, out var side))
            {
                Debug.LogWarning($"[TrainTimetableStop] 入線側が未指定のため既定へ倒した side={(int)Side} pos={StationPosition.Vector3Int}");
            }
            if (!TrainTimetableWireMapping.TryToDepartureConditionType(DepartureCondition, out var departureConditionType))
            {
                Debug.LogWarning($"[TrainTimetableStop] 出発条件が未指定のため既定へ倒した condition={(int)DepartureCondition} pos={StationPosition.Vector3Int}");
            }
            return new TrainTimetableStop(StationPosition.Vector3Int, side, departureConditionType, WaitTicks);
        }
    }
}
