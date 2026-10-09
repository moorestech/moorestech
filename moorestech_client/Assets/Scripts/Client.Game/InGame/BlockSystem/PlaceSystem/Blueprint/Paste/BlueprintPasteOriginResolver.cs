using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using UnityEngine;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     ヒット面からBP外接箱の原点を解決する
    ///     Resolves blueprint extent origins from hit surfaces
    /// </summary>
    public static class BlueprintPasteOriginResolver
    {
        internal static bool TryResolveCursorOrigin(Camera camera, Vector3Int footprintSize, int heightOffset, out Vector3Int anchor, out Vector3Int origin, out PlacementHitSurfaceKind surfaceKind)
        {
            origin = default;
            surfaceKind = PlacementHitSurfaceKind.Ground;
            if (!PlaceSystemUtil.TryGetRayHitPlacePointBySize(camera, footprintSize, heightOffset,
                    out anchor, out var surface, out var hitPoint)) return false;

            // 通常設置の原点へBP固有の側面補正だけ適用する
            // Apply only the blueprint side-face adjustment to the shared origin
            surfaceKind = surface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace;
            anchor = AlignSideFace(anchor, hitPoint, surface == null ? null : surface.PreviewSurfaceType, heightOffset);
            return TryResolveFootprintOrigin(anchor, footprintSize, heightOffset, surfaceKind, out origin);
        }

        internal static bool TryResolveFootprintOrigin(Vector3Int anchor, Vector3Int footprintSize, int heightOffset,
            PlacementHitSurfaceKind surfaceKind, out Vector3Int origin)
        {
            origin = anchor;
            if (surfaceKind == PlacementHitSurfaceKind.BlockFace) return true;

            // ドラッグ始点も底面最高点へ補正し縦列へ保持する
            // Preserve a footprint-ground-adjusted drag start for vertical runs
            return PlacementGroundCellResolver.TryResolveCellFromGround(origin, BlockDirection.North, footprintSize,
                heightOffset, out origin);
        }

        public static Vector3Int ResolveOrigin(Vector3Int footprintSize, Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset)
        {
            // 外接箱を通常セル規則で解決
            // Resolve the extent with ordinary cell rules.
            var origin = PlaceSystemUtil.CalcPlacePointBySize(footprintSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
            return AlignSideFace(origin, hitPoint, surfaceType, heightOffset);
        }

        private static Vector3Int AlignSideFace(Vector3Int origin, Vector3 hitPoint, PreviewSurfaceType? surfaceType, int heightOffset)
        {
            if (surfaceType is not (PreviewSurfaceType.YX_Origin or PreviewSurfaceType.YX_Z or PreviewSurfaceType.YZ_Origin or PreviewSurfaceType.YZ_X)) return origin;

            // 側面だけ最下段をカーソル段へそろえる
            // Align only side-face bottoms to the cursor's level
            return new Vector3Int(origin.x, Mathf.FloorToInt(hitPoint.y) + heightOffset, origin.z);
        }
    }
}
