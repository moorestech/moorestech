using System;
using ClassLibrary;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    public class PlaceSystemUtil
    {
        public static bool TryGetRayHitBlockPosition(Camera mainCamera, int heightOffset, BlockDirection currentBlockDirection, out Vector3Int pos, out BlockPreviewBoundingBoxSurface surface)
        {
            pos = Vector3Int.zero;
            surface = null;
            
            if (!TryGetRayHitPosition(mainCamera, out var hitPos, out surface)) return false;
            
            pos = CalcPlacePoint(hitPos, heightOffset, currentBlockDirection, surface);
            
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
        
        public static bool TryGetRaySpecifiedComponentHit<T>(Camera mainCamera, out T component) where T : class
        {
            component = null;
            var ray = mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit, float.PositiveInfinity, LayerConst.Without_Player_MapObject_Block_LayerMask)) return false;
            //そのrayが指定されたコンポーネントを持っているか
            if (!hit.transform.TryGetComponent(out component))
            {
                return false;
            }
            
            return true;
        }
        
        public static Vector3Int CalcPlacePoint(Vector3 hitPoint, int heightOffset, BlockDirection currentBlockDirection, BlockPreviewBoundingBoxSurface boundingBoxSurface)
        {
            var holdingBlockMaster = boundingBoxSurface.BlockGameObject.BlockMasterElement;
            var rotateAction = currentBlockDirection.GetCoordinateConvertAction();
            var rotatedSize = rotateAction(holdingBlockMaster.BlockSize).Abs();
            
            if (boundingBoxSurface == null)
            {
                var point = Vector3Int.zero;
                point.x = Mathf.FloorToInt(hitPoint.x + (rotatedSize.x % 2 == 0 ? 0.5f : 0));
                point.z = Mathf.FloorToInt(hitPoint.z + (rotatedSize.z % 2 == 0 ? 0.5f : 0));
                point.y = Mathf.FloorToInt(hitPoint.y);
                
                point += new Vector3Int(0, heightOffset, 0);
                point -= new Vector3Int(rotatedSize.x, 0, rotatedSize.z) / 2;
                
                return point;
            }
            
            switch (boundingBoxSurface.PreviewSurfaceType)
            {
                case PreviewSurfaceType.YX_Origin:
                    return new Vector3Int(
                        Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                        Mathf.FloorToInt(hitPoint.y),
                        Mathf.FloorToInt(hitPoint.z) - Mathf.RoundToInt(rotatedSize.z / 2f)
                    );
                case PreviewSurfaceType.YX_Z:
                    return new Vector3Int(
                        Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                        Mathf.FloorToInt(hitPoint.y),
                        Mathf.FloorToInt(hitPoint.z)
                    );
                case PreviewSurfaceType.YZ_Origin:
                    return new Vector3Int(
                        Mathf.FloorToInt(hitPoint.x) - Mathf.RoundToInt(rotatedSize.x / 2f),
                        Mathf.FloorToInt(hitPoint.y),
                        Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                    );
                case PreviewSurfaceType.YZ_X:
                    return new Vector3Int(
                        Mathf.FloorToInt(hitPoint.x),
                        Mathf.FloorToInt(hitPoint.y),
                        Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                    );
                
                case PreviewSurfaceType.XZ_Origin:
                    return new Vector3Int(
                        Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                        Mathf.FloorToInt(hitPoint.y) - rotatedSize.y,
                        Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                    );
                case PreviewSurfaceType.XZ_Y:
                    return new Vector3Int(
                        Mathf.FloorToInt(hitPoint.x) - Mathf.FloorToInt(rotatedSize.x / 2f),
                        Mathf.FloorToInt(hitPoint.y),
                        Mathf.FloorToInt(hitPoint.z) - Mathf.FloorToInt(rotatedSize.z / 2f)
                    );
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}