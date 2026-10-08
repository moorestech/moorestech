using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    ///     カーソル下の1x1セルを通常設置と同じ規約で決める
    ///     Resolves the 1x1 cursor cell with the normal placement rule
    /// </summary>
    public static class PlacementUnitCellResolver
    {
        private static readonly Vector3Int UnitSize = Vector3Int.one;

        public static bool TryGetCursorCell(Camera mainCamera, int heightOffset, out Vector3Int cell)
        {
            cell = Vector3Int.zero;
            if (!PlaceSystemUtil.TryRaycastPlacementSurface(mainCamera, out var hit, out var surface)) return false;

            // 地面ヒットだけ地形の高さ格子を渡す
            // Only a ground hit passes the terrain height lattice step
            var groundHeightQuantizationStep = surface == null ? GroundHeightQuantization.StepOf(hit.collider) : 0f;
            var surfaceType = surface == null ? (PreviewSurfaceType?)null : surface.PreviewSurfaceType;
            cell = ResolveCell(hit.point, surfaceType, groundHeightQuantizationStep, heightOffset);
            return true;
        }

        public static Vector3Int ResolveCell(Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset)
        {
            return PlaceSystemUtil.CalcPlacePointBySize(UnitSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
        }
    }
}
