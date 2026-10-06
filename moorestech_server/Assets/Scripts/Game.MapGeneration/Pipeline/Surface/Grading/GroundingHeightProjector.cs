using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    // 整地面の投影。core代入は生成時と表示時の両方、skirt補間は生成時の1回だけ使う
    // Pad projection: core assignment serves both generation and display, skirt blending runs once at generation
    internal static class GroundingHeightProjector
    {
        // 生成時の合成入口。skirtを元高さから合成した後でcoreを代入する
        // The generation-time composition: blend skirts from the original heights, then assign cores
        internal static float[,] ApplyAtGeneration(float[,] heights, Vector2 tileScene, Vector2 spacing, float terrainHeight,
            IReadOnlyList<VeinGroundingPad> pads)
        {
            var skirted = ApplySkirts(heights, tileScene, spacing, terrainHeight, pads);
            return ApplyCores(skirted, tileScene, spacing, terrainHeight, pads);
        }

        // core支持頂点へpad高さを代入する。重なりは辞書順で先のpadが勝つ
        // Assign pad heights to core support vertices; on overlap the lexicographically first pad wins
        internal static float[,] ApplyCores(float[,] heights, Vector2 tileScene, Vector2 spacing, float terrainHeight,
            IReadOnlyList<VeinGroundingPad> pads)
        {
            var layout = new PadLayout(heights, tileScene, spacing, pads);
            var result = (float[,])heights.Clone();
            for (int z = 0; z < layout.Lattice.Depth; z++)
            for (int x = 0; x < layout.Lattice.Width; x++)
            {
                int owner = layout.CoreOwner(x, z);
                if (0 <= owner) result[z, x] = layout.Ordered[owner].HeightMeters / terrainHeight;
            }
            return result;
        }

        // core外頂点を元高さ基準で全skirt同時合成する。入力順で結果を変えない
        // Blend every skirt over non-core vertices from the original heights, independent of input order
        // BlendWidthが格子間隔より狭いと支持の外の頂点はcoreから格子間隔以上離れ、重みが0でskirtは何も変えない
        // When BlendWidth is narrower than the lattice spacing, vertices past the support sit a full spacing from the core, so the weight is 0 and skirts change nothing
        internal static float[,] ApplySkirts(float[,] heights, Vector2 tileScene, Vector2 spacing, float terrainHeight,
            IReadOnlyList<VeinGroundingPad> pads)
        {
            var layout = new PadLayout(heights, tileScene, spacing, pads);
            var result = (float[,])heights.Clone();
            for (int z = 0; z < layout.Lattice.Depth; z++)
            for (int x = 0; x < layout.Lattice.Width; x++)
            {
                if (0 <= layout.CoreOwner(x, z)) continue;
                var scene = layout.Lattice.ScenePosition(x, z);
                double weightSum = 0d;
                double weightedHeight = 0d;
                float strongest = 0f;
                foreach (int i in layout.Spatial.At(x, z))
                {
                    // core外頂点はChebyshev接続
                    // Connect non-support vertices by Chebyshev distance
                    var pad = layout.Ordered[i];
                    float dx = Mathf.Max(pad.Core.xMin - scene.x, scene.x - pad.Core.xMax, 0f);
                    float dz = Mathf.Max(pad.Core.yMin - scene.y, scene.y - pad.Core.yMax, 0f);
                    float distance = Mathf.Max(dx, dz);
                    float weight = 1f - Mathf.SmoothStep(0f, 1f, distance / pad.BlendWidth);
                    weightSum += weight;
                    weightedHeight += weight * (double)pad.HeightMeters;
                    strongest = Mathf.Max(strongest, weight);
                }
                if (0d < weightSum)
                    result[z, x] = Mathf.Lerp(heights[z, x], (float)(weightedHeight / weightSum) / terrainHeight, strongest);
            }
            return result;
        }

        private sealed class PadLayout
        {
            public readonly List<VeinGroundingPad> Ordered;
            public readonly SurfaceLattice Lattice;
            public readonly GroundingPadSpatialIndex Spatial;
            private readonly RectInt[] _supports;

            public PadLayout(float[,] heights, Vector2 tileScene, Vector2 spacing, IReadOnlyList<VeinGroundingPad> pads)
            {
                Ordered = new List<VeinGroundingPad>(pads);
                Ordered.Sort(new PadOrder());
                Lattice = new SurfaceLattice(tileScene, spacing, heights.GetLength(1), heights.GetLength(0));
                Spatial = new GroundingPadSpatialIndex(Lattice, Ordered);
                _supports = new RectInt[Ordered.Count];
                for (int i = 0; i < Ordered.Count; i++) _supports[i] = Lattice.SupportVertices(Ordered[i].Core);
            }

            public int CoreOwner(int x, int z)
            {
                foreach (int i in Spatial.At(x, z))
                    if (_supports[i].Contains(new Vector2Int(x, z))) return i;
                return -1;
            }
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
