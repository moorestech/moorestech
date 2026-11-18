using System;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Ui.State.Train.Logic
{
    // 時刻表へ入れられる駅（Web の TrainTimetableStation）
    // A station that can join the timetable (Web TrainTimetableStation)
    public readonly struct TrainTimetableStation
    {
        public Vector3Int Position { get; }
        public string Name { get; }

        public TrainTimetableStation(Vector3Int position, string name)
        {
            Position = position;
            Name = name;
        }
    }

    // 時刻表の停車駅 1 件。Side は入線する端（Web の TrainTimetableStop）
    // One timetable stop; Side is the arrival end (Web TrainTimetableStop)
    public readonly struct TimetableEditStop
    {
        public Vector3Int Position { get; }
        public string Name { get; }
        public StationNodeSide Side { get; }

        public TimetableEditStop(Vector3Int position, string name, StationNodeSide side)
        {
            Position = position;
            Name = name;
            Side = side;
        }
    }

    // 置き換え送信の停車駅 1 件（Web の train_timetable.replace の stops 要素）
    // One stop of the replace request (an element of Web train_timetable.replace stops)
    public readonly struct TimetableReplaceStop : IEquatable<TimetableReplaceStop>
    {
        public Vector3Int Position { get; }
        public StationNodeSide Side { get; }

        public TimetableReplaceStop(Vector3Int position, StationNodeSide side)
        {
            Position = position;
            Side = side;
        }

        public bool Equals(TimetableReplaceStop other) => Position == other.Position && Side == other.Side;
        public override bool Equals(object obj) => obj is TimetableReplaceStop other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Position, (int)Side);
        public override string ToString() => $"{Position}:{Side}";
    }

    // 上下移動の向き。値は並びの番号の増減
    // Move direction; the value is the index delta
    public enum StopMoveDirection
    {
        Up = -1,
        Down = 1,
    }
}
