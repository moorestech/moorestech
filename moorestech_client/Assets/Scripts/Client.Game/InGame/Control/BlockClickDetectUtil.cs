using Client.Common;
using Client.Game.InGame.Block;
using UnityEngine;
using UnityEngine.InputSystem;

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

            // InputSystemのマウス座標を使う（実機と入力注入の双方を同一経路で扱う）
            // Use the Input System mouse position so real and injected input share one path
            var mousePosition = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition;
            var ray = camera.ScreenPointToRay(mousePosition);
            
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.BlockOnlyLayerMask)) return false;
            component = hit.collider.gameObject.GetComponentInChildren<T>();
            
            return component is not null;
        }
    }
}