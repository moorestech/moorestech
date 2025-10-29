using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    public class PlaceSystemUtil
    {
        public static bool TryGetRayHitPosition(Camera mainCamera , out Vector3 pos,out BlockPreviewBoundingBoxSurface surface)
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
            
            //基本的にブロックの原点は0,0なので、rayがヒットした座標を基準にブロックの原点を計算する
            pos = hit.point;
            
            return true;
        }
    }
}