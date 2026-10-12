using System.Collections.Generic;
using UnityEngine;
using Game.MapGeneration.Pipeline.Visual.Placement;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    internal sealed class GroundingPadSpatialIndex
    {
        private const int BucketWidth = 16;
        private readonly Dictionary<Vector2Int, List<int>> _buckets = new();
        private readonly List<int> _empty = new();

        internal GroundingPadSpatialIndex(SurfaceLattice lattice, IReadOnlyList<VeinGroundingPad> pads)
        {
            for (int index = 0; index < pads.Count; index++)
            {
                var pad = pads[index];
                var support = lattice.SupportVertices(VeinGroundingPlanner.OuterOf(pad.Core, pad.BlendWidth));

                // pad外周のbucketへ辞書順で登録
                // Register sorted pad indices in the perimeter buckets
                for (int z = support.yMin / BucketWidth; z <= (support.yMax - 1) / BucketWidth; z++)
                for (int x = support.xMin / BucketWidth; x <= (support.xMax - 1) / BucketWidth; x++)
                {
                    var key = new Vector2Int(x, z);
                    if (!_buckets.TryGetValue(key, out var entries))
                    {
                        entries = new List<int>();
                        _buckets.Add(key, entries);
                    }
                    entries.Add(index);
                }
            }
        }

        internal IReadOnlyList<int> At(int x, int z)
        {
            return _buckets.TryGetValue(new Vector2Int(x / BucketWidth, z / BucketWidth), out var entries) ? entries : _empty;
        }
    }
}
