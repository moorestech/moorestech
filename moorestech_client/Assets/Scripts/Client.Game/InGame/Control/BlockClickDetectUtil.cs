using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Control.BuildView;
using UnityEngine;

namespace Client.Game.InGame.Control
{
    public static class BlockClickDetectUtil
    {
        public static bool TryGetCursorOnBlockPosition(out Vector3Int position)
        {
            position = Vector3Int.zero;
            
            if (!TryGetCursorOnBlock(out var blockObject)) return false;
            
            
            position = blockObject.BlockPosInfo.OriginalPos;
            
            return true;
        }
        
        public static bool TryGetCursorOnBlock(out BlockGameObject blockObject)
        {
            blockObject = null;
            
            if (!TryGetCursorOnComponent<BlockGameObjectChild>(out var child)) return false;
            
            blockObject = child.BlockGameObject;
            
            return true;
        }
        
        
        /// <summary>
        /// 25/11/4 列車エンティティとブロックのインタラクト判定の共通化のために一旦こうしたが、本当にこれで良いのだろうか、、、要検討
        /// </summary>
        public static bool TryGetCursorOnComponent<T>(out T component)
        {
            component = default;
            
            // 25/11/4 そもそもCamera.mainを使ってていいのか？これも検討したい
            var camera = Camera.main;
            if (camera == null) return false;
            
            // 照準座標はAimPointProviderで視点モードに応じて一元解決する
            // The aim point is resolved centrally by AimPointProvider per view mode
            var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
            
            var hits = Physics.RaycastAll(ray, 100, LayerConst.BlockOnlyLayerMask);
            if (hits.Length == 0) return false;
            
            // 手前のプレビューゴーストだけを貫通対象にする
            // Only nearby preview ghosts are allowed to be penetrated
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<BlockPreviewObject>() != null) continue;
                
                component = hit.collider.gameObject.GetComponentInChildren<T>();
                if (component is not null) return true;
                
                return false;
            }
            
            return false;
        }
    }
}
