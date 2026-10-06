using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    // タイル境界を含む単一のシーン格子を定義する
    // Define one scene lattice including tile boundaries
    public sealed class SurfaceLattice
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Spacing;
        public readonly int Width;
        public readonly int Depth;

        public SurfaceLattice(Vector2 origin, Vector2 spacing, int width, int depth)
        {
            Origin = origin;
            Spacing = spacing;
            Width = width;
            Depth = depth;
        }

        public Vector2 ScenePosition(int x, int z)
        {
            return Origin + new Vector2(x * Spacing.x, z * Spacing.y);
        }

        public Vector2 GridPosition(Vector2 scene)
        {
            return new Vector2((scene.x - Origin.x) / Spacing.x, (scene.y - Origin.y) / Spacing.y);
        }

        public bool Contains(Rect footprint)
        {
            return SurfaceGenerationValidation.Finite(footprint.xMin) && SurfaceGenerationValidation.Finite(footprint.yMin) &&
                   SurfaceGenerationValidation.Finite(footprint.xMax) && SurfaceGenerationValidation.Finite(footprint.yMax) &&
                   footprint.width >= 0f && footprint.height >= 0f &&
                   footprint.xMin >= Origin.x && footprint.yMin >= Origin.y &&
                   footprint.xMax <= Origin.x + (Width - 1) * Spacing.x &&
                   footprint.yMax <= Origin.y + (Depth - 1) * Spacing.y;
        }

        public RectInt SupportVertices(Rect footprint)
        {
            var minimum = GridPosition(footprint.min);
            var maximum = GridPosition(footprint.max);

            // 格子線に接する両側の補間セルも支持領域へ含める
            // Include interpolation cells on both sides of a touching grid line
            int xMin = Mathf.Max(0, Mathf.CeilToInt(minimum.x) - 1);
            int zMin = Mathf.Max(0, Mathf.CeilToInt(minimum.y) - 1);
            int xMax = Mathf.Min(Width - 1, Mathf.FloorToInt(maximum.x) + 1);
            int zMax = Mathf.Min(Depth - 1, Mathf.FloorToInt(maximum.y) + 1);
            return new RectInt(xMin, zMin, xMax - xMin + 1, zMax - zMin + 1);
        }
    }
}
