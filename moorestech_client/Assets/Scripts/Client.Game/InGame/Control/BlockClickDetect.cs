using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
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
        
        public static bool IsClickOpenableBlock(IPlacementPreviewBlockGameObjectController blockPlacePreview)
        {
            if (blockPlacePreview.IsActive) return false; //ブロック設置中の場合は無効
            if (TryGetClickBlock(out var block))
            {
                return block.BlockMasterElement.IsBlockOpenable();
            }
            
            return false;
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
        public static bool TryGetCursorOnComponent<T>(out T component) where T : Component
        {
            component = null;
            
            // 25/11/4 そもそもCamera.mainを使ってていいのか？これも検討したい
            var camera = Camera.main;
            if (camera == null) return false;
            
            //TODO InputSystemのリファクタ対象
            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            
            if (!Physics.Raycast(ray, out var hit, 100, LayerConst.BlockOnlyLayerMask)) return false;
            component = hit.collider.gameObject.GetComponent<T>();
            
            return component is not null;
        }
    }
}