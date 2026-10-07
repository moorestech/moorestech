using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public sealed class LandCellField
    {
        public readonly SurfaceLattice Geometry;
        private readonly bool[] _land;

        public LandCellField(SurfaceLattice geometry, bool[] land)
        {
            Geometry = geometry;
            _land = (bool[])land.Clone();
        }

        internal bool IsLandVertex(int x, int z)
        {
            return _land[z * Geometry.Width + x];
        }

        public bool ContainsSupport(Rect sceneFootprint)
        {
            if (!Geometry.Contains(sceneFootprint)) return false;
            var support = Geometry.SupportVertices(sceneFootprint);

            // 元分類だけで整地候補を判定し、持上げ済みの高さを陸と誤認しない
            // Judge grading candidates from original classification, never from raised heights
            for (int z = support.yMin; z < support.yMax; z++)
            for (int x = support.xMin; x < support.xMax; x++)
                if (!_land[z * Geometry.Width + x]) return false;
            return true;
        }

        public bool IsProtectedVertex(int globalX, int globalZ)
        {
            // 角のどれかが陸のセルは全頂点を保護する
            // Protect every vertex of cells with at least one land corner
            for (int z = Mathf.Max(0, globalZ - 1); z <= Mathf.Min(Geometry.Depth - 1, globalZ + 1); z++)
            for (int x = Mathf.Max(0, globalX - 1); x <= Mathf.Min(Geometry.Width - 1, globalX + 1); x++)
                if (_land[z * Geometry.Width + x]) return true;
            return false;
        }
    }
}
