using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    public static class GroundingHeightProjector
    {
        public static float[,] Apply(float[,] heights, Vector2 tileScene, Vector2 spacing, float terrainHeight,
            IReadOnlyList<VeinGroundingPad> pads)
        {
            var result = (float[,])heights.Clone();
            var ordered = new List<VeinGroundingPad>(pads);
            ordered.Sort(new PadOrder());
            var lattice = new SurfaceLattice(tileScene, spacing, heights.GetLength(1), heights.GetLength(0));
            var spatial = new GroundingPadSpatialIndex(lattice, ordered);
            var supports = new RectInt[ordered.Count];
            for (int i = 0; i < ordered.Count; i++) supports[i] = lattice.SupportVertices(ordered[i].Core);

            // 元高さを基準に全skirtを同時合成し、入力順で結果を変えない
            // Compose all skirts simultaneously from original heights, independent of input order
            for (int z = 0; z < lattice.Depth; z++)
            for (int x = 0; x < lattice.Width; x++)
            {
                var scene = lattice.ScenePosition(x, z);
                double weightSum = 0d;
                double weightedHeight = 0d;
                float strongest = 0f;
                bool core = false;
                foreach (int i in spatial.At(x, z))
                {
                    var pad = ordered[i];
                    if (supports[i].Contains(new Vector2Int(x, z)))
                    {
                        result[z, x] = pad.HeightMeters / terrainHeight;
                        core = true;
                        break;
                    }

                    // core支持頂点以外はChebyshev距離で接続する
                    // Connect other vertices using the Chebyshev distance from the core
                    float dx = Mathf.Max(pad.Core.xMin - scene.x, scene.x - pad.Core.xMax, 0f);
                    float dz = Mathf.Max(pad.Core.yMin - scene.y, scene.y - pad.Core.yMax, 0f);
                    float distance = Mathf.Max(dx, dz);
                    float weight = 1f - Mathf.SmoothStep(0f, 1f, distance / pad.BlendWidth);
                    weightSum += weight;
                    weightedHeight += weight * (double)pad.HeightMeters;
                    strongest = Mathf.Max(strongest, weight);
                }
                if (!core && weightSum > 0d)
                    result[z, x] = Mathf.Lerp(heights[z, x], (float)(weightedHeight / weightSum) / terrainHeight, strongest);
            }
            return result;
        }

        private sealed class PadOrder : IComparer<VeinGroundingPad>
        {
            public int Compare(VeinGroundingPad a, VeinGroundingPad b)
            {
                int result = a.Core.xMin.CompareTo(b.Core.xMin);
                if (result == 0) result = a.Core.yMin.CompareTo(b.Core.yMin);
                if (result == 0) result = a.Core.xMax.CompareTo(b.Core.xMax);
                if (result == 0) result = a.Core.yMax.CompareTo(b.Core.yMax);
                return result;
            }
        }
    }
}
