using System.Collections.Generic;
using Core.Master;

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
        public RailGraphConnectionSnapshot(int fromNodeId, int toNodeId, int distance, ItemId railItemId)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Distance = distance;
            RailItemId = railItemId;
        }

        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public int Distance { get; }
        public ItemId RailItemId { get; }
    }
}
