using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     RailGraph全体のスナップショットを表現するDTO
    /// </summary>
    public readonly struct RailGraphSnapshot
    {
        public RailGraphSnapshot(
            IReadOnlyList<RailNodeInitializationNotifier.RailNodeInitializationData> nodes,
            IReadOnlyList<RailGraphConnectionSnapshot> connections,
            uint connectNodesHash,
            long graphTick)
        {
            Nodes = nodes;
            Connections = connections;
            ConnectNodesHash = connectNodesHash;
            GraphTick = graphTick;
        }

        public IReadOnlyList<RailNodeInitializationNotifier.RailNodeInitializationData> Nodes { get; }
        public IReadOnlyList<RailGraphConnectionSnapshot> Connections { get; }
        public uint ConnectNodesHash { get; }
        public long GraphTick { get; }
    }

    public readonly struct RailGraphConnectionSnapshot
    {
        public RailGraphConnectionSnapshot(int fromNodeId, int toNodeId, int distance)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Distance = distance;
        }

        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public int Distance { get; }
    }
}
