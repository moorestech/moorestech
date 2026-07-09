using Client.Common;
using Client.Game.InGame.Block;
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
            
            // プレビューゴーストを自然に飛ばすため、手前から順に対象コンポーネントを探す
            // Search target components from nearest hits so preview ghosts are skipped naturally
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            
            foreach (var hit in hits)
            {
                component = hit.collider.gameObject.GetComponentInChildren<T>();
                if (component is not null) return true;
            }
            
            return false;
        }
    }
}
