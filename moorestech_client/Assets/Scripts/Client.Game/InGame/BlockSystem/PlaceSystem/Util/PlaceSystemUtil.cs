using System;
using System.Collections.Generic;
using ClassLibrary;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    public class PlaceSystemUtil
    {
        public static bool TryGetRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, BlockMasterElement holdingBlock, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            pos = Vector3Int.zero;
            surface = null;
            
            if (!TryGetRayHitPosition(mainCamera, out var hitPos, out surface)) return false;
            
            pos = CalcPlacePoint(holdingBlock, hitPos, heightOffset, currentBlockDirection, surface);
            
            return true;
        }
        
        
        public static bool TryGetRayHitPosition(Camera mainCamera, out Vector3 pos, out BlockPreviewBoundingBoxSurface surface)
        {
            surface = null;
            pos = Vector3Int.zero;
            var ray = mainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, LayerConst.Without_Player_MapObject_Block_LayerMask)) return false;
            //そのrayが地面のオブジェクトかブロックのバウンディングボックスにヒットしてるか
            if (
                !hit.transform.TryGetComponent<GroundGameObject>(out _) &&
                !hit.transform.TryGetComponent(out surface)
            )
            {
                return false;
            }
            
            pos = hit.point;

            return true;
        }

        public static Vector3Int SnapHitPointToCell(Vector3 hitPoint)
        {
            // BPコピーと貼り付けで共通のセル化規約（XZは床スナップ、Yは整数グリッド面の丸め）
            // Shared cell-snap convention for BP copy and paste: floor XZ, round Y on the integer grid face
            return new Vector3Int(Mathf.FloorToInt(hitPoint.x), Mathf.RoundToInt(hitPoint.y), Mathf.FloorToInt(hitPoint.z));
        }

        public static bool TryGetRaySpecifiedComponentHit<T>(Camera mainCamera, out T component, int layerMask) where T : class
        {
            component = null;
            var ray = mainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, layerMask)) return false;
            //そのrayが指定されたコンポーネントを持っているか
            if (!hit.transform.TryGetComponent(out component))
            {
                return false;
            }
            
            return true;
        }
        
        public static bool TryGetRaySpecifiedComponentHitPosition<T>(Camera mainCamera, out Vector3 pos, out T component, int layerMask) where T : class
        {
            component = null;
            pos = Vector3Int.zero;
            var ray = mainCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, layerMask)) return false;
            //そのrayが指定されたコンポーネントを持っているか
            if (!hit.transform.TryGetComponent(out component))
            {
                return false;
            }
            pos = hit.point;
            return true;
        }
        
        public static Vector3Int CalcPlacePoint(BlockMasterElement holdingBlock ,Vector3 hitPoint, int heightOffset, BlockDirection currentBlockDirection, BlockPreviewBoundingBoxSurface boundingBoxSurface)
        {
            PreviewSurfaceType? surfaceType = boundingBoxSurface == null ? (PreviewSurfaceType?)null : boundingBoxSurface.PreviewSurfaceType;
            return CalcPlacePoint(holdingBlock, hitPoint, heightOffset, currentBlockDirection, surfaceType);
        }

        public static Vector3Int CalcPlacePoint(BlockMasterElement holdingBlock, Vector3 hitPoint, int heightOffset, BlockDirection currentBlockDirection, PreviewSurfaceType? surfaceType)
        {
            var rotateAction = currentBlockDirection.GetCoordinateConvertAction();
            var rotatedSize = rotateAction(holdingBlock.BlockSize).Abs();

            if (surfaceType == null)
            {
                var point = Vector3Int.zero;
                point.x = Mathf.FloorToInt(hitPoint.x + (rotatedSize.x % 2 == 0 ? 0.5f : 0));
                point.z = Mathf.FloorToInt(hitPoint.z + (rotatedSize.z % 2 == 0 ? 0.5f : 0));

                // 天面ヒットのyは整数ちょうどだが、掠り角レイの浮動小数点誤差で僅かに下回り1段沈むためイプシロン補正
                // Top-face hits land exactly on integer y, but grazing rays dip epsilon below and sink one cell, so correct it
                point.y = Mathf.FloorToInt(hitPoint.y + 0.001f);

                point += new Vector3Int(0, heightOffset, 0);
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
        
        public static void SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos)
        {
            // セル毎BlockId付きでPlaceInfoをサーバーに送信
            // Send PlaceInfo to server; each cell already carries its own BlockId
            ClientContext.VanillaApi.SendOnly.PlaceBlock(currentPlaceInfos);

            // Ctrl+Z用に設置バッチを履歴へ記録する（全セル設置不能の空バッチは積まない）
            // Record the place batch into the undo history for Ctrl+Z (skip empty batches where no cell was placeable)
            var record = PlaceOperationRecord.CreateFrom(currentPlaceInfos);
            if (record.HasCells) ClientDIContext.BuildOperationHistory.Push(record);

            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
        }
    }
}