using System;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailComponent座標とFront/Back情報の組を表す簡易データ
    ///     Lightweight representation of a rail component endpoint and its front/back flag
    /// </summary>
    public readonly struct ConnectionDestination : IEquatable<ConnectionDestination>
    {
        public static readonly ConnectionDestination Default = new ConnectionDestination(new Vector3Int(-1, -1, -1), -1, true);

        public ConnectionDestination(Vector3Int componentPosition, int componentIndex, bool isFront)
        {
            ComponentPosition = componentPosition;
            ComponentIndex = componentIndex;
            IsFront = isFront;
        }

        public Vector3Int ComponentPosition { get; }

        public int ComponentIndex { get; }

        public bool IsFront { get; }

        public bool IsDefault => ComponentIndex < 0;

        public bool Equals(ConnectionDestination other)
        {
            return ComponentIndex == other.ComponentIndex && IsFront == other.IsFront && ComponentPosition == other.ComponentPosition;
        }

        public override bool Equals(object obj)
        {
            return obj is ConnectionDestination other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ComponentPosition.GetHashCode();
                hash = (hash * 397) ^ ComponentIndex;
                hash = (hash * 397) ^ IsFront.GetHashCode();
                return hash;
            }
        }
    }
}
