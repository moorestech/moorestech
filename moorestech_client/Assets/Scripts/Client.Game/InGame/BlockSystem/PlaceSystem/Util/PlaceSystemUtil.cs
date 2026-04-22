using System;
using System.Collections.Generic;
using ClassLibrary;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using Client.Game.InGame.SoundEffect;
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
            var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
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
        
        public static bool TryGetRaySpecifiedComponentHit<T>(Camera mainCamera, out T component, int layerMask) where T : class
        {
            component = null;
            var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
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
            var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
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
                point.y = Mathf.FloorToInt(hitPoint.y);

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
            switch (surfaceType.Value)
            {
                case PreviewSurfaceType.YX_Origin:
                    // 既存ブロックの-Z面 → 新ブロックは-Z方向へ、原点は face - size
                    // -Z face → new block origin = face - size
                    return new Vector3Int(SnapParallelX(), SnapParallelY(), Mathf.RoundToInt(hitPoint.z) - rotatedSize.z);
                case PreviewSurfaceType.YX_Z:
                    // 既存ブロックの+Z面 → 新ブロックは+Z方向へ、原点は face
                    // +Z face → new block origin = face
                    return new Vector3Int(SnapParallelX(), SnapParallelY(), Mathf.RoundToInt(hitPoint.z));
                case PreviewSurfaceType.YZ_Origin:
                    return new Vector3Int(Mathf.RoundToInt(hitPoint.x) - rotatedSize.x, SnapParallelY(), SnapParallelZ());
                case PreviewSurfaceType.YZ_X:
                    return new Vector3Int(Mathf.RoundToInt(hitPoint.x), SnapParallelY(), SnapParallelZ());
                case PreviewSurfaceType.XZ_Origin:
                    return new Vector3Int(SnapParallelX(), Mathf.RoundToInt(hitPoint.y) - rotatedSize.y, SnapParallelZ());
                case PreviewSurfaceType.XZ_Y:
                    return new Vector3Int(SnapParallelX(), Mathf.RoundToInt(hitPoint.y), SnapParallelZ());
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public static void SendPlaceProtocol(List<PlaceInfo> currentPlaceInfos, PlaceSystemUpdateContext context)
        {
            // PlaceInfoをサーバーに送信
            // Send PlaceInfo to server
            ClientContext.VanillaApi.SendOnly.PlaceHotBarBlock(currentPlaceInfos, context.CurrentSelectHotbarSlotIndex);
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
        }
    }
}