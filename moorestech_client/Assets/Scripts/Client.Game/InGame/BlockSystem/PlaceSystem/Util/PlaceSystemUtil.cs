using System;
using System.Collections.Generic;
using ClassLibrary;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Player;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    public class PlaceSystemUtil
    {
        // 全PlaceSystem共通の設置距離
        // Placement distance shared by all PlaceSystems
        private const float PlaceableMaxDistance = 100f;

        // プレイヤー位置基準で設置距離を判定する（起点をカメラ位置にすると視点の引き方で判定が食い違う）
        // Judge placeable distance from the player position (a camera-based origin would disagree as the view is pulled back)
        public static bool IsPlaceableFromPlayer(Vector3Int placePoint)
        {
            var placePosition = (Vector3)placePoint;
            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;

            return Vector3.Distance(playerPosition, placePosition) <= PlaceableMaxDistance;
        }

        public static bool TryGetRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            var rotatedSize = currentBlockDirection.GetCoordinateConvertAction()(holdingBlock.BlockSize).Abs();
            return TryGetRayHitPlacePointBySize(mainCamera, rotatedSize, heightOffset, out pos, out surface);
        }

        // 通常設置とBPのセル解決が共有する、レイキャスト→設置セルの本体
        // Shared raycast-to-placement-cell body for normal placement and blueprint cell resolution
        public static bool TryGetRayHitPlacePointBySize(Camera mainCamera, Vector3Int rotatedSize, int heightOffset, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            pos = Vector3Int.zero;
            if (!TryRaycastPlacementSurface(mainCamera, out var hit, out surface)) return false;

            // 地面ヒットだけ、当たった地形の高さ格子1段をY決定へ渡す
            // Only a ground hit hands the hit terrain's height lattice step to the Y decision
            var groundHeightQuantizationStep = surface == null ? GroundHeightQuantization.StepOf(hit.collider) : 0f;
            var surfaceType = surface == null ? (PreviewSurfaceType?)null : surface.PreviewSurfaceType;
            pos = CalcPlacePointBySize(rotatedSize, hit.point, heightOffset, surfaceType, groundHeightQuantizationStep);
            return true;
        }

        public static bool TryGetRayHitPosition(Camera mainCamera, out Vector3 pos, out BlockPreviewBoundingBoxSurface surface)
        {
            pos = Vector3Int.zero;
            if (!TryRaycastPlacementSurface(mainCamera, out var hit, out surface)) return false;

            pos = hit.point;
            return true;
        }

        internal static bool TryRaycastPlacementSurface(Camera mainCamera, out RaycastHit hit, out BlockPreviewBoundingBoxSurface surface)
        {
            surface = null;
            var ray = mainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());

            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out hit, float.PositiveInfinity, LayerConst.Without_Player_MapObject_Block_LayerMask)) return false;
            //そのrayが地面のオブジェクトかブロックのバウンディングボックスにヒットしてるか
            return hit.transform.TryGetComponent<GroundGameObject>(out _) || hit.transform.TryGetComponent(out surface);
        }

        public static Vector3Int SnapHitPointToCell(Vector3 hitPoint)
        {
            // 列車車両の距離判定専用。BPの設置セル解決にはPlacementUnitCellResolverを使う
            // Only for train-car distance checks; blueprint placement cells use PlacementUnitCellResolver
            return new Vector3Int(Mathf.FloorToInt(hitPoint.x), Mathf.RoundToInt(hitPoint.y), Mathf.FloorToInt(hitPoint.z));
        }

        public static Vector3Int CalcPlacePoint(BlockMasterElement holdingBlock ,Vector3 hitPoint, int heightOffset, BlockDirection currentBlockDirection, BlockPreviewBoundingBoxSurface boundingBoxSurface, float groundHeightQuantizationStep)
        {
            PreviewSurfaceType? surfaceType = boundingBoxSurface == null ? (PreviewSurfaceType?)null : boundingBoxSurface.PreviewSurfaceType;
            return CalcPlacePoint(holdingBlock, hitPoint, heightOffset, currentBlockDirection, surfaceType, groundHeightQuantizationStep);
        }

        // groundHeightQuantizationStepは地面ヒット（surfaceType==null）でだけ使う、当たった地形の高さ格子1段
        // groundHeightQuantizationStep is the hit terrain's height lattice step, used only for a ground hit (surfaceType == null)
        public static Vector3Int CalcPlacePoint(BlockMasterElement holdingBlock, Vector3 hitPoint, int heightOffset, BlockDirection currentBlockDirection, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep)
        {
            var rotateAction = currentBlockDirection.GetCoordinateConvertAction();
            var rotatedSize = rotateAction(holdingBlock.BlockSize).Abs();
            return CalcPlacePointBySize(rotatedSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
        }

        // 回転済みサイズで通常設置とBPのセル解決を共通化する
        // Resolve normal placement and blueprint cells from the same rotated size
        public static Vector3Int CalcPlacePointBySize(Vector3Int rotatedSize, Vector3 hitPoint, int heightOffset, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep)
        {
            if (surfaceType == null)
            {
                var point = Vector3Int.zero;
                point.x = Mathf.FloorToInt(hitPoint.x + (rotatedSize.x % 2 == 0 ? 0.5f : 0));
                point.z = Mathf.FloorToInt(hitPoint.z + (rotatedSize.z % 2 == 0 ? 0.5f : 0));

                // 地面ヒットのYは地形追従と同じ規約で決め、TerrainDataの格子で整数を僅かに下回る地表を1段沈めない
                // A ground hit's Y follows the terrain-follow rule, so a surface just under an integer on the TerrainData lattice does not sink a cell
                point.y = PlacementGroundCellResolver.ResolveCellY(hitPoint.y, groundHeightQuantizationStep, heightOffset);
                point -= new Vector3Int(rotatedSize.x, 0, rotatedSize.z) / 2;

                return point;
            }

            // 面に平行な軸（=面上のヒット位置）は偶数/奇数サイズで中央寄せスナップ
            // Axes parallel to the face snap with size-parity center alignment
            int SnapParallelX() => Mathf.FloorToInt(hitPoint.x + (rotatedSize.x % 2 == 0 ? 0.5f : 0)) - rotatedSize.x / 2;
            int SnapParallelY() => Mathf.FloorToInt(hitPoint.y + (rotatedSize.y % 2 == 0 ? 0.5f : 0)) - rotatedSize.y / 2;
            int SnapParallelZ() => Mathf.FloorToInt(hitPoint.z + (rotatedSize.z % 2 == 0 ? 0.5f : 0)) - rotatedSize.z / 2;

            // 面に垂直な軸は面が整数グリッド上にあるためRoundToIntで浮動小数点誤差を吸収
            // Axis perpendicular to the face uses RoundToInt to absorb floating-point imprecision (face is on integer grid)
            var snapped = surfaceType.Value switch
            {
                // 既存ブロックの-Z面 → 新ブロックは-Z方向へ、原点は face - size
                // -Z face → new block origin = face - size
                PreviewSurfaceType.YX_Origin => new Vector3Int(SnapParallelX(), SnapParallelY(), Mathf.RoundToInt(hitPoint.z) - rotatedSize.z),
                // 既存ブロックの+Z面 → 新ブロックは+Z方向へ、原点は face
                // +Z face → new block origin = face
                PreviewSurfaceType.YX_Z => new Vector3Int(SnapParallelX(), SnapParallelY(), Mathf.RoundToInt(hitPoint.z)),
                PreviewSurfaceType.YZ_Origin => new Vector3Int(Mathf.RoundToInt(hitPoint.x) - rotatedSize.x, SnapParallelY(), SnapParallelZ()),
                PreviewSurfaceType.YZ_X => new Vector3Int(Mathf.RoundToInt(hitPoint.x), SnapParallelY(), SnapParallelZ()),
                PreviewSurfaceType.XZ_Origin => new Vector3Int(SnapParallelX(), Mathf.RoundToInt(hitPoint.y) - rotatedSize.y, SnapParallelZ()),
                PreviewSurfaceType.XZ_Y => new Vector3Int(SnapParallelX(), Mathf.RoundToInt(hitPoint.y), SnapParallelZ()),
                _ => throw new ArgumentOutOfRangeException(),
            };

            // Q/Eの上下オフセットを面ヒット時にも一括で反映する
            // Apply Q/E vertical offset uniformly even when hitting an existing block face
            return snapped + new Vector3Int(0, heightOffset, 0);
        }
    }
}