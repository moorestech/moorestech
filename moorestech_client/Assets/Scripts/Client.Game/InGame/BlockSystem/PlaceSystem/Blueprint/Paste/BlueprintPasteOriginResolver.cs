using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    public static class BlueprintPasteOriginResolver
    {
        public static bool TryResolveCursorOrigin(Camera camera, Vector3Int footprintSize, int heightOffset, out Vector3Int origin, out PlacementHitSurfaceKind surfaceKind)
        {
            origin = default;
            surfaceKind = PlacementHitSurfaceKind.Ground;
            if (!PlaceSystemUtil.TryRaycastPlacementSurface(camera, out var hit, out var surface)) return false;

            // 通常設置と同じヒット面と地形格子を使う
            // Use the same hit surface and terrain lattice as normal placement
            surfaceKind = surface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace;
            var step = surface == null ? GroundHeightQuantization.StepOf(hit.collider) : 0f;
            var surfaceType = surface == null ? (PreviewSurfaceType?)null : surface.PreviewSurfaceType;
            origin = ResolveOrigin(footprintSize, hit.point, surfaceType, step, heightOffset);
            return true;
        }

        public static Vector3Int ResolveOrigin(Vector3Int footprintSize, Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset)
        {
            // 外接箱を大きなブロックとして通常のセル解決へ渡す
            // Resolve the extent as one large block using the normal cell rule
            var origin = PlaceSystemUtil.CalcPlacePointBySize(footprintSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
            if (!IsSideFace(surfaceType)) return origin;

            // 側面だけ最下段をカーソル段へそろえる
            // Align only side-face bottoms to the cursor's level
            return new Vector3Int(origin.x, Mathf.FloorToInt(hitPoint.y) + heightOffset, origin.z);

            #region Internal

            static bool IsSideFace(PreviewSurfaceType? type) => type is PreviewSurfaceType.YX_Origin or PreviewSurfaceType.YX_Z or PreviewSurfaceType.YZ_Origin or PreviewSurfaceType.YZ_X;

            #endregion
        }
    }
}
