using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    // ダイクストラ結果を距離と経路IDのペアで返すDTO
    // DTO that bundles Dijkstra outputs with total distance and path ids
    public readonly struct RailPathResult
    {
        public static readonly RailPathResult Empty = new RailPathResult(Array.Empty<int>(), -1);

        public RailPathResult(IReadOnlyList<int> path, int distance)
        {
            Path = path;
            Distance = distance;
        }

        public IReadOnlyList<int> Path { get; }
        public int Distance { get; }
    }
}
