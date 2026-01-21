using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    // レール探索に必要な接続情報とノード解決を提供する
    // Provides adjacency data and node resolution for rail traversal
    public interface IRailGraphTraversalProvider
    {
        IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> ConnectNodes { get; }
        bool TryGetNode(int nodeId, out IRailNode node);
    }
}
