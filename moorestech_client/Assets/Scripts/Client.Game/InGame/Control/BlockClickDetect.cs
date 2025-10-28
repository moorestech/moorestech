using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Game.InGame.Control
{
    public static class BlockClickDetect
    {
        public static bool TryGetCursorOnBlockPosition(out Vector3Int position)
        {
            position = Vector3Int.zero;
            
            if (!TryGetCursorOnBlock(out var blockObject)) return false;
            
            
            position = blockObject.BlockPosInfo.OriginalPos;
            
            return true;
        }
        
        public static bool TryGetClickBlockPosition(out Vector3Int position)
        {
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && TryGetCursorOnBlockPosition(out position)) return true;
            
            position = Vector3Int.zero;
            return false;
        }
        
        public static bool TryGetClickBlock(out BlockGameObject blockObject)
        {
            blockObject = null;
            // UIのクリックかどうかを判定
            if (EventSystem.current.IsPointerOverGameObject()) return false;
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && TryGetCursorOnBlock(out blockObject)) return true;
            
            blockObject = null;
            return false;
        }
        
        public static bool IsClickOpenableBlock()
        {
            if (TryGetClickBlock(out var block))
            {
                return block.BlockMasterElement.IsBlockOpenable();
            }
            
            return false;
        }
        
        public static bool TryGetCursorOnBlock(out BlockGameObject blockObject)
        {
            blockObject = null;
            
            var camera = Camera.main;
            if (camera == null) return false;
            
            //TODO InputSystemのリファクタ対象
            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.BlockOnlyLayerMask)) return false;
            var child = hit.collider.gameObject.GetComponent<BlockGameObjectChild>();
            if (child is null) return false;
            
            
            blockObject = child.BlockGameObject;
            
            return true;
        }
    }
}