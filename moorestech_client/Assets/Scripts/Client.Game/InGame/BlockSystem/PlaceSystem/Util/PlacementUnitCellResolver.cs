using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
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
            return PlaceSystemUtil.TryGetRayHitPlacePointBySize(mainCamera, UnitSize, heightOffset, out cell, out _);
        }

        public static Vector3Int ResolveCell(Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset)
        {
            return PlaceSystemUtil.CalcPlacePointBySize(UnitSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
        }
    }
}
