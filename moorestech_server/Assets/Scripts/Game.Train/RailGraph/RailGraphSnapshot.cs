using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     RailGraph全体のスナップショットを表現するDTO
    /// </summary>
    public readonly struct RailGraphSnapshot
    {
        public RailGraphSnapshot(
            IReadOnlyList<RailNodeInitializationData> nodes,
            IReadOnlyList<RailGraphConnectionSnapshot> connections,
            uint connectNodesHash,
            uint graphTick)
        {
            Nodes = nodes;
            Connections = connections;
            ConnectNodesHash = connectNodesHash;
            GraphTick = graphTick;
        }

        public IReadOnlyList<RailNodeInitializationData> Nodes { get; }
        public IReadOnlyList<RailGraphConnectionSnapshot> Connections { get; }
        public uint ConnectNodesHash { get; }
        public uint GraphTick { get; }
    }

    public readonly struct RailGraphConnectionSnapshot
    {
        public RailGraphConnectionSnapshot(int fromNodeId, int toNodeId, int distance, Guid railTypeGuid, bool isDrawable)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Distance = distance;
            RailTypeGuid = railTypeGuid;
            IsDrawable = isDrawable;
        }

        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public int Distance { get; }
        public Guid RailTypeGuid { get; }
        public bool IsDrawable { get; }
    }
}
