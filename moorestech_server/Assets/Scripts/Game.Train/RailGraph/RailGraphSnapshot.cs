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
            long graphTick)
        {
            Nodes = nodes;
            Connections = connections;
            ConnectNodesHash = connectNodesHash;
            GraphTick = graphTick;
        }

        public IReadOnlyList<RailNodeInitializationData> Nodes { get; }
        public IReadOnlyList<RailGraphConnectionSnapshot> Connections { get; }
        public uint ConnectNodesHash { get; }
        public long GraphTick { get; }
    }

    public readonly struct RailGraphConnectionSnapshot
    {
        public RailGraphConnectionSnapshot(int fromNodeId, int toNodeId, int distance, Guid railTypeGuid)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Distance = distance;
            RailTypeGuid = railTypeGuid;
        }

        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public int Distance { get; }
        public Guid RailTypeGuid { get; }
    }
}
