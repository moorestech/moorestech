using Client.Game.InGame.Control.ViewMode;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    public static class PlaceSystemRaycastUtil
    {
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
        
    }
}
