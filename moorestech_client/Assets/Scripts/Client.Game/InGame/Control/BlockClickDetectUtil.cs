using Client.Common;
using Client.Game.InGame.Block;
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
            
            //TODO InputSystemのリファクタ対象
            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.BlockOnlyLayerMask)) return false;
            component = hit.collider.gameObject.GetComponentInChildren<T>();
            Debug.Log(component);
            
            return component is not null;
        }
    }
}