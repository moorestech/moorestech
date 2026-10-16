using System;
using System.Collections.Generic;
using ClassLibrary;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Player;
using Core.Master;
using Game.Block.Interface;
using Game.PlacementTarget;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    public class PlaceSystemUtil
    {
        // プレイヤー位置基準で設置距離を判定する（起点をカメラ位置にすると視点の引き方で判定が食い違う）
        // Judge placeable distance from the player position (a camera-based origin would disagree as the view is pulled back)
        public static bool IsPlaceableFromPlayer(Vector3Int placePoint)
        {
            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;

            return PlacementDistanceRule.IsWithinReach(playerPosition, placePoint);
        }

        public static bool TryGetRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            var rotatedSize = currentBlockDirection.GetCoordinateConvertAction()(holdingBlock.BlockSize).Abs();
            return TryGetRayHitPlacePointBySize(mainCamera, rotatedSize, heightOffset, out pos, out surface);
        }

        // 地形ゲート付き設置系の1セル版。地面ヒットのYを占有セルの地形最高点で決め直す（ADR 0047・0080）
        // Single-cell entry for terrain-gated placements; a ground hit's Y is re-decided from the footprint terrain max (ADR 0047, 0080)
        public static FootprintGroundedCellResult ResolveFootprintGroundedRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            if (!TryGetRayHitBlockPosition(mainCamera, heightOffset, currentBlockDirection, holdingBlock, out var rayHitCell, out surface))
            {
                pos = rayHitCell;
                return FootprintGroundedCellResult.NoPlacementSurface;
            }

            var surfaceKind = surface == null ? PlacementHitSurfaceKind.Ground : PlacementHitSurfaceKind.BlockFace;
            return ResolveFootprintGroundedCell(rayHitCell, surfaceKind, currentBlockDirection, holdingBlock.BlockSize, heightOffset, out pos);
        }

        // 理由行を積める呼び出し側向け。地表なしは理由を積んで不可、照準面なしは理由なしで不可
        // For callers holding feedback; no ground pushes its reason, no aim surface fails without a line
        public static bool TryGetFootprintGroundedRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, PlacementFeedback feedback, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            var result = ResolveFootprintGroundedRayHitBlockPosition(mainCamera, heightOffset, currentBlockDirection, holdingBlock, out pos, out surface);
            if (result == FootprintGroundedCellResult.GroundNotFound) feedback.AddGroundNotFound();
            // silent-ok: 照準が地面にもブロックにも当たらないのは操作中の通常状態 / プレビューが出ないことでプレイヤーが気づく
            return result == FootprintGroundedCellResult.Resolved;
        }

        internal static FootprintGroundedCellResult ResolveFootprintGroundedCell(Vector3Int rayHitCell, PlacementHitSurfaceKind surfaceKind, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int pos)
        {
            pos = rayHitCell;

            // ブロック面ヒット（積み重ね）は整数グリッド上なので触らない
            // Block-face hits (stacking) sit on the integer grid and stay untouched
            if (surfaceKind == PlacementHitSurfaceKind.BlockFace) return FootprintGroundedCellResult.Resolved;

            // 占有セルの地表が取れなければ置けないので、その理由を呼び出し側へ返す
            // Without ground under the footprint the block cannot be placed, so that reason goes back to the caller
            if (!PlacementGroundCellResolver.TryResolveCellFromGround(rayHitCell, blockDirection, blockSize, heightOffset, out pos)) return FootprintGroundedCellResult.GroundNotFound;
            return FootprintGroundedCellResult.Resolved;
        }

        // 通常設置とBPのセル解決が共有する、レイキャスト→設置セルの本体
        // Shared raycast-to-placement-cell body for normal placement and blueprint cell resolution
        public static bool TryGetRayHitPlacePointBySize(Camera mainCamera, Vector3Int rotatedSize, int heightOffset, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            return TryGetRayHitPlacePointBySize(mainCamera, rotatedSize, heightOffset, out pos, out surface, out _);
        }

        internal static bool TryGetRayHitPlacePointBySize(Camera mainCamera, Vector3Int rotatedSize, int heightOffset,
            out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface, out Vector3 hitPoint)
        {
            pos = Vector3Int.zero;
            hitPoint = default;
            if (!TryRaycastPlacementSurface(mainCamera, out var hit, out surface)) return false;
            hitPoint = hit.point;

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

        // 回転済みサイズで通常設置とBPのセル解決を共通化する
        // Resolve normal placement and blueprint cells from the same rotated size
        // groundHeightQuantizationStepは地面ヒット（surfaceType==null）でだけ使う、当たった地形の高さ格子1段
        // groundHeightQuantizationStep is the hit terrain's height lattice step, used only for a ground hit (surfaceType == null)
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